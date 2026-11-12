using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Intranet.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Agentic;

/// <summary>
/// One-time, idempotent removal of the seeded wireframe catalog (fixed ids and
/// <c>dev-*</c> owner oids). Never deletes rows owned by a real Entra user oid.
/// </summary>
public sealed class WorkspaceDemoCleanup
{
    private readonly IntranetDbContext _db;
    private readonly AgenticOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<WorkspaceDemoCleanup> _logger;

    public WorkspaceDemoCleanup(
        IntranetDbContext db,
        IOptions<AgenticOptions> options,
        IWebHostEnvironment environment,
        ILogger<WorkspaceDemoCleanup> logger)
    {
        _db = db;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var removed = 0;
        var demoOids = WorkspaceIds.DemoOwnerOids.ToArray();
        var demoChatIds = WorkspaceIds.DemoChatIds.ToArray();
        var demoProjectIds = WorkspaceIds.DemoProjectIds.ToArray();
        var demoShareIds = WorkspaceIds.DemoShareIds.ToArray();
        var demoDocumentIds = WorkspaceIds.DemoDocumentIds.ToArray();
        var demoFolderIds = WorkspaceIds.DemoFolderIds.Where(id => id != WorkspaceIds.FolderRoot).ToArray();
        removed += await DeleteDemoSharesAsync(demoOids, demoShareIds, cancellationToken);
        removed += await DeleteDemoChatsAsync(demoOids, demoChatIds, cancellationToken);
        removed += await DeleteDemoProjectsAsync(demoOids, demoProjectIds, cancellationToken);
        removed += await DeleteDemoDocumentsAsync(demoDocumentIds, cancellationToken);
        removed += await DeleteEmptyDemoFoldersAsync(demoFolderIds, cancellationToken);
        await EnsureRootFolderAsync(cancellationToken);
        if (removed > 0)
        {
            _logger.LogInformation("Removed {Count} seeded agentic workspace demo rows.", removed);
        }

        return removed;
    }

    public async Task EnsureRootFolderAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.AgenticRepoFolders.AnyAsync(f => f.ParentId == null, cancellationToken))
        {
            return;
        }

        _db.AgenticRepoFolders.Add(new AgenticRepoFolder
        {
            Id = WorkspaceIds.FolderRoot,
            ParentId = null,
            Name = "Root",
            Path = "Root",
            IsPublic = true,
            IsIndexed = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created empty workspace repository root.");
    }

    private async Task<int> DeleteDemoSharesAsync(
        string[] demoOids,
        Guid[] demoShareIds,
        CancellationToken cancellationToken)
    {
        var chatShares = await _db.AgenticChatShares
            .Where(s =>
                demoShareIds.Contains(s.Id)
                || demoOids.Contains(s.PrincipalOid)
                || demoOids.Contains(s.CreatedByOid))
            .ToListAsync(cancellationToken);
        var projectShares = await _db.AgenticProjectShares
            .Where(s => demoOids.Contains(s.PrincipalOid) || demoOids.Contains(s.CreatedByOid))
            .ToListAsync(cancellationToken);
        if (chatShares.Count == 0 && projectShares.Count == 0)
        {
            return 0;
        }

        _db.AgenticChatShares.RemoveRange(chatShares);
        _db.AgenticProjectShares.RemoveRange(projectShares);
        await _db.SaveChangesAsync(cancellationToken);
        return chatShares.Count + projectShares.Count;
    }

    private async Task<int> DeleteDemoChatsAsync(
        string[] demoOids,
        Guid[] demoChatIds,
        CancellationToken cancellationToken)
    {
        var chats = await _db.AgenticChats
            .Where(c => demoChatIds.Contains(c.Id) || demoOids.Contains(c.OwnerOid))
            .ToListAsync(cancellationToken);
        if (chats.Count == 0)
        {
            return 0;
        }

        _db.AgenticChats.RemoveRange(chats);
        await _db.SaveChangesAsync(cancellationToken);
        return chats.Count;
    }

    private async Task<int> DeleteDemoProjectsAsync(
        string[] demoOids,
        Guid[] demoProjectIds,
        CancellationToken cancellationToken)
    {
        var projects = await _db.AgenticProjects
            .Where(p => demoProjectIds.Contains(p.Id) || demoOids.Contains(p.OwnerOid))
            .ToListAsync(cancellationToken);
        if (projects.Count == 0)
        {
            return 0;
        }

        _db.AgenticProjects.RemoveRange(projects);
        await _db.SaveChangesAsync(cancellationToken);
        return projects.Count;
    }

    private async Task<int> DeleteDemoDocumentsAsync(Guid[] demoDocumentIds, CancellationToken cancellationToken)
    {
        var documents = await _db.AgenticRepoDocuments
            .Where(d => demoDocumentIds.Contains(d.Id))
            .ToListAsync(cancellationToken);
        if (documents.Count == 0)
        {
            return 0;
        }

        var root = RepositoryStorage.ResolveRoot(_options, _environment);
        foreach (var document in documents)
        {
            TryDeleteStoredFile(root, document.StoragePath);
        }

        _db.AgenticRepoDocuments.RemoveRange(documents);
        await _db.SaveChangesAsync(cancellationToken);
        return documents.Count;
    }

    private async Task<int> DeleteEmptyDemoFoldersAsync(Guid[] demoFolderIds, CancellationToken cancellationToken)
    {
        var folders = await _db.AgenticRepoFolders
            .Where(f => demoFolderIds.Contains(f.Id))
            .ToListAsync(cancellationToken);
        if (folders.Count == 0)
        {
            return 0;
        }

        var remainingDocs = await _db.AgenticRepoDocuments
            .Where(d => demoFolderIds.Contains(d.FolderId))
            .Select(d => d.FolderId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var occupied = remainingDocs.ToHashSet();
        var removed = 0;
        var changed = true;
        while (changed)
        {
            changed = false;
            var empty = folders
                .Where(f =>
                    !occupied.Contains(f.Id)
                    && !folders.Any(child => child.ParentId == f.Id))
                .ToList();
            if (empty.Count == 0)
            {
                break;
            }

            _db.AgenticRepoFolders.RemoveRange(empty);
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var folder in empty)
            {
                folders.Remove(folder);
            }

            removed += empty.Count;
            changed = true;
        }

        return removed;
    }

    private static void TryDeleteStoredFile(string root, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return;
        }

        try
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
            {
                File.Delete(full);
            }
        }
        catch
        {
            // Cleanup must not fail startup if a leftover file is locked.
        }
    }
}
