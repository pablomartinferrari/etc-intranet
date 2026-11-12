using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Agentic;

public sealed partial class WorkspaceService
{
    public async Task<IReadOnlyList<WorkspaceChatDto>> ListFolderChatsAsync(
        WorkspaceActor actor,
        Guid folderId,
        CancellationToken cancellationToken)
    {
        _ = await _db.AgenticRepoFolders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken)
            ?? throw new AgenticException("Folder not found.", StatusCodes.Status404NotFound);
        var chats = await VisibleChats(actor, cancellationToken);
        return chats.Where(c => c.FolderId == folderId).ToList();
    }

    public async Task<RepoFolderDto> CreateFolderAsync(
        WorkspaceActor actor,
        Guid parentId,
        string name,
        CancellationToken cancellationToken)
    {
        RequireSignedIn(actor);
        var parent = await _db.AgenticRepoFolders.FirstOrDefaultAsync(f => f.Id == parentId, cancellationToken)
            ?? throw new AgenticException("Folder not found.", StatusCodes.Status404NotFound);
        var safe = RepositoryNames.NormalizeFolderName(name);
        await EnsureUniqueFolderNameAsync(parent.Id, safe, excludeId: null, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var folder = new AgenticRepoFolder
        {
            Id = Guid.NewGuid(),
            ParentId = parent.Id,
            Name = safe,
            Path = RepositoryNames.CombinePath(parent.Path, safe),
            IsPublic = true,
            IsIndexed = true,
            CreatedAt = now,
            CreatedByOid = actor.Oid,
            CreatedByName = actor.DisplayName,
            UpdatedAt = now,
            UpdatedByOid = actor.Oid,
            UpdatedByName = actor.DisplayName,
        };
        _db.AgenticRepoFolders.Add(folder);
        await _db.SaveChangesAsync(cancellationToken);
        var root = RepositoryStorage.ResolveRoot(_options, _environment);
        Directory.CreateDirectory(RepositoryStorage.FolderDirectory(root, folder));
        return ToFolderDto(folder, 0);
    }

    public async Task<RepoFolderDto> RenameFolderAsync(
        WorkspaceActor actor,
        Guid folderId,
        string name,
        CancellationToken cancellationToken)
    {
        RequireSignedIn(actor);
        var folder = await RequireMutableFolderAsync(folderId, cancellationToken);
        var safe = RepositoryNames.NormalizeFolderName(name);
        if (string.Equals(folder.Name, safe, StringComparison.Ordinal))
        {
            return ToFolderDto(folder, folder.Children.Count);
        }

        await EnsureUniqueFolderNameAsync(folder.ParentId, safe, folder.Id, cancellationToken);
        var root = RepositoryStorage.ResolveRoot(_options, _environment);
        var oldDir = RepositoryStorage.FolderDirectory(root, folder);
        folder.Name = safe;
        await RewriteDescendantPathsAsync(folder, cancellationToken);
        await SyncDocumentFolderPathsAsync(folder.Id, cancellationToken);
        Touch(folder, actor);
        await _db.SaveChangesAsync(cancellationToken);
        MoveDirectoryIfNeeded(oldDir, RepositoryStorage.FolderDirectory(root, folder));
        return ToFolderDto(folder, folder.Children.Count);
    }

    public async Task<RepoFolderDto> MoveFolderAsync(
        WorkspaceActor actor,
        Guid folderId,
        Guid parentId,
        CancellationToken cancellationToken)
    {
        RequireSignedIn(actor);
        var folder = await RequireMutableFolderAsync(folderId, cancellationToken);
        if (folder.Id == parentId)
        {
            throw new AgenticException("A folder cannot be moved into itself.", StatusCodes.Status400BadRequest);
        }

        var parent = await _db.AgenticRepoFolders.FirstOrDefaultAsync(f => f.Id == parentId, cancellationToken)
            ?? throw new AgenticException("Destination folder not found.", StatusCodes.Status404NotFound);
        var all = await _db.AgenticRepoFolders.ToListAsync(cancellationToken);
        if (CollectSubtree(all, folder.Id).Any(f => f.Id == parent.Id))
        {
            throw new AgenticException("A folder cannot be moved into one of its descendants.", StatusCodes.Status400BadRequest);
        }

        await EnsureUniqueFolderNameAsync(parent.Id, folder.Name, folder.Id, cancellationToken);
        var root = RepositoryStorage.ResolveRoot(_options, _environment);
        var oldDir = RepositoryStorage.FolderDirectory(root, folder);
        folder.ParentId = parent.Id;
        folder.Parent = parent;
        await RewriteDescendantPathsAsync(folder, cancellationToken);
        await SyncDocumentFolderPathsAsync(folder.Id, cancellationToken);
        Touch(folder, actor);
        await _db.SaveChangesAsync(cancellationToken);
        MoveDirectoryIfNeeded(oldDir, RepositoryStorage.FolderDirectory(root, folder));
        return ToFolderDto(folder, folder.Children.Count);
    }

    public async Task DeleteFolderAsync(
        WorkspaceActor actor,
        Guid folderId,
        bool recursive,
        CancellationToken cancellationToken)
    {
        RequireSignedIn(actor);
        var folder = await RequireMutableFolderAsync(folderId, cancellationToken);
        var all = await _db.AgenticRepoFolders.ToListAsync(cancellationToken);
        var subtree = CollectSubtree(all, folder.Id);
        var subtreeIds = subtree.Select(f => f.Id).ToHashSet();
        var docs = await _db.AgenticRepoDocuments.Where(d => subtreeIds.Contains(d.FolderId)).ToListAsync(cancellationToken);
        var childFolders = subtree.Count - 1;
        if (!recursive && (docs.Count > 0 || childFolders > 0))
        {
            throw new AgenticException(
                $"Folder is not empty ({childFolders} subfolder(s), {docs.Count} file(s)). " +
                "Pass recursive=true to delete it. Chats in this folder are moved to My chats.",
                StatusCodes.Status409Conflict);
        }

        var chats = await _db.AgenticChats.Where(c => c.FolderId != null && subtreeIds.Contains(c.FolderId.Value)).ToListAsync(cancellationToken);
        foreach (var chat in chats)
        {
            chat.FolderId = null;
            chat.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var root = RepositoryStorage.ResolveRoot(_options, _environment);
        foreach (var document in docs)
        {
            await _search.RemoveDocumentAsync(document.Id, cancellationToken);
            DeleteStoredFile(root, document.StoragePath);
        }

        _db.AgenticRepoDocuments.RemoveRange(docs);
        foreach (var child in subtree.Where(f => f.Id != folder.Id).OrderByDescending(f => f.Path.Length))
        {
            _db.AgenticRepoFolders.Remove(child);
        }

        _db.AgenticRepoFolders.Remove(folder);
        await _db.SaveChangesAsync(cancellationToken);
        TryDeleteDirectory(RepositoryStorage.FolderDirectory(root, folder));
    }

    public async Task<RepoDocumentDto> RenameDocumentAsync(
        WorkspaceActor actor,
        Guid documentId,
        string name,
        CancellationToken cancellationToken)
    {
        RequireSignedIn(actor);
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        var safe = RepositoryNames.NormalizeFileName(name);
        await EnsureUniqueDocumentNameAsync(document.FolderId, safe, document.Id, cancellationToken);
        document.Name = safe;
        document.ModifiedAt = DateTimeOffset.UtcNow;
        document.UpdatedByOid = actor.Oid;
        document.UpdatedByName = actor.DisplayName;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDocumentDto(document, document.Folder.Path);
    }

    public async Task<RepoDocumentDto> MoveDocumentAsync(
        WorkspaceActor actor,
        Guid documentId,
        Guid folderId,
        CancellationToken cancellationToken)
    {
        RequireSignedIn(actor);
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        var folder = await _db.AgenticRepoFolders.FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken)
            ?? throw new AgenticException("Destination folder not found.", StatusCodes.Status404NotFound);
        await EnsureUniqueDocumentNameAsync(folder.Id, document.Name, document.Id, cancellationToken);
        var root = RepositoryStorage.ResolveRoot(_options, _environment);
        var oldFull = RepositoryStorage.CombineUnderRoot(root, document.StoragePath);
        var destDir = RepositoryStorage.FolderDirectory(root, folder);
        Directory.CreateDirectory(destDir);
        var destName = Path.GetFileName(document.StoragePath);
        if (string.IsNullOrWhiteSpace(destName))
        {
            destName = $"{document.Id:N}{Path.GetExtension(document.Name)}";
        }

        var destFull = Path.Combine(destDir, destName);
        if (File.Exists(oldFull) && !string.Equals(oldFull, destFull, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(oldFull, destFull, overwrite: true);
            document.StoragePath = Path.GetRelativePath(root, destFull).Replace('\\', '/');
        }

        document.FolderId = folder.Id;
        document.Folder = folder;
        document.FolderPath = folder.Path;
        document.ModifiedAt = DateTimeOffset.UtcNow;
        document.UpdatedByOid = actor.Oid;
        document.UpdatedByName = actor.DisplayName;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDocumentDto(document, folder.Path);
    }

    public async Task DeleteDocumentAsync(WorkspaceActor actor, Guid documentId, CancellationToken cancellationToken)
    {
        RequireSignedIn(actor);
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        var root = RepositoryStorage.ResolveRoot(_options, _environment);
        await _search.RemoveDocumentAsync(document.Id, cancellationToken);
        DeleteStoredFile(root, document.StoragePath);
        _db.AgenticRepoDocuments.Remove(document);
        await _db.SaveChangesAsync(cancellationToken);
    }

    internal async Task EnsureUniqueFolderNameAsync(
        Guid? parentId,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var siblings = await _db.AgenticRepoFolders
            .Where(f => f.ParentId == parentId)
            .ToListAsync(cancellationToken);
        if (siblings.Any(f => f.Id != excludeId && f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AgenticException("A folder with that name already exists here.", StatusCodes.Status409Conflict);
        }
    }

    internal async Task EnsureUniqueDocumentNameAsync(
        Guid folderId,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var siblings = await _db.AgenticRepoDocuments
            .Where(d => d.FolderId == folderId)
            .ToListAsync(cancellationToken);
        if (siblings.Any(d => d.Id != excludeId && d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AgenticException("A file with that name already exists in this folder.", StatusCodes.Status409Conflict);
        }
    }

    private async Task<AgenticRepoFolder> RequireMutableFolderAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var folder = await _db.AgenticRepoFolders
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken)
            ?? throw new AgenticException("Folder not found.", StatusCodes.Status404NotFound);
        if (folder.ParentId is null)
        {
            throw new AgenticException("The repository root cannot be renamed, moved, or deleted.", StatusCodes.Status400BadRequest);
        }

        return folder;
    }

    private async Task<AgenticRepoDocument> RequireDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        await _db.AgenticRepoDocuments
            .Include(d => d.Folder)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
        ?? throw new AgenticException("File not found.", StatusCodes.Status404NotFound);

    private async Task RewriteDescendantPathsAsync(AgenticRepoFolder folder, CancellationToken cancellationToken)
    {
        var parentPath = folder.ParentId is { } parentId
            ? (await _db.AgenticRepoFolders.AsNoTracking().FirstAsync(f => f.Id == parentId, cancellationToken)).Path
            : "Root";
        if (folder.ParentId is null)
        {
            parentPath = "";
        }

        folder.Path = folder.ParentId is null ? folder.Name : RepositoryNames.CombinePath(parentPath, folder.Name);
        var all = await _db.AgenticRepoFolders.ToListAsync(cancellationToken);
        var byParent = all.GroupBy(f => f.ParentId ?? Guid.Empty).ToDictionary(g => g.Key, g => g.ToList());
        ApplyChildPaths(folder, byParent);
    }

    private async Task SyncDocumentFolderPathsAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var all = await _db.AgenticRepoFolders.ToListAsync(cancellationToken);
        var subtreeIds = CollectSubtree(all, folderId).Select(f => f.Id).ToHashSet();
        var paths = all.Where(f => subtreeIds.Contains(f.Id)).ToDictionary(f => f.Id, f => f.Path);
        var docs = await _db.AgenticRepoDocuments.Where(d => subtreeIds.Contains(d.FolderId)).ToListAsync(cancellationToken);
        foreach (var document in docs)
        {
            if (paths.TryGetValue(document.FolderId, out var path))
            {
                document.FolderPath = path;
            }
        }
    }

    private static void ApplyChildPaths(AgenticRepoFolder parent, IReadOnlyDictionary<Guid, List<AgenticRepoFolder>> byParent)
    {
        if (!byParent.TryGetValue(parent.Id, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            child.Path = RepositoryNames.CombinePath(parent.Path, child.Name);
            ApplyChildPaths(child, byParent);
        }
    }

    private static List<AgenticRepoFolder> CollectSubtree(IReadOnlyList<AgenticRepoFolder> all, Guid rootId)
    {
        var map = all.ToDictionary(f => f.Id);
        var ids = new HashSet<Guid> { rootId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var folder in all)
            {
                if (folder.ParentId is { } parent && ids.Contains(parent) && ids.Add(folder.Id))
                {
                    changed = true;
                }
            }
        }

        return ids.Select(id => map[id]).ToList();
    }

    private static void Touch(AgenticRepoFolder folder, WorkspaceActor actor)
    {
        folder.UpdatedAt = DateTimeOffset.UtcNow;
        folder.UpdatedByOid = actor.Oid;
        folder.UpdatedByName = actor.DisplayName;
    }

    private static void MoveDirectoryIfNeeded(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(from))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (Directory.Exists(to))
        {
            return;
        }

        Directory.Move(from, to);
    }

    private static void DeleteStoredFile(string root, string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return;
        }

        try
        {
            var full = RepositoryStorage.CombineUnderRoot(root, storagePath);
            if (File.Exists(full))
            {
                File.Delete(full);
            }
        }
        catch (AgenticException)
        {
            /* ignore paths that fail the root check */
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
