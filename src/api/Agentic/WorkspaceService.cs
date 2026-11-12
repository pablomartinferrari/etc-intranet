using System.Text.Json;
using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Intranet.Api.Data;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Agentic;

public interface IWorkspaceService
{
    WorkspaceCapabilitiesDto Capabilities();

    Task<WorkspaceSidebarDto> GetSidebarAsync(WorkspaceActor actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceProjectDto>> ListProjectsAsync(WorkspaceActor actor, CancellationToken cancellationToken);

    Task<WorkspaceProjectDto> GetProjectAsync(WorkspaceActor actor, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepoDocumentDto>> ListProjectFilesAsync(WorkspaceActor actor, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceChatDto>> ListProjectChatsAsync(WorkspaceActor actor, Guid projectId, CancellationToken cancellationToken);

    Task<WorkspaceProjectDto> CreateProjectAsync(WorkspaceActor actor, CreateProjectRequestDto request, CancellationToken cancellationToken);

    Task<WorkspaceProjectDto> UpdateProjectAsync(WorkspaceActor actor, Guid projectId, UpdateProjectRequestDto request, CancellationToken cancellationToken);

    Task<WorkspaceChatDto> CreateChatAsync(WorkspaceActor actor, CreateChatRequestDto request, CancellationToken cancellationToken);

    Task<WorkspaceChatDto> GetChatAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken);

    Task<WorkspaceChatDto> UpdateChatAsync(WorkspaceActor actor, Guid chatId, UpdateChatRequestDto request, CancellationToken cancellationToken);

    Task DeleteChatAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken);

    Task LeaveChatAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceMessageDto>> ListMessagesAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken);

    Task<(WorkspaceMessageDto User, WorkspaceMessageDto Assistant)> SendAsync(
        WorkspaceActor actor,
        Guid chatId,
        string text,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatShareDto>> ListSharesAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken);

    Task<ChatShareDto> AddShareAsync(WorkspaceActor actor, Guid chatId, CreateShareRequestDto request, CancellationToken cancellationToken);

    Task RemoveShareAsync(WorkspaceActor actor, Guid chatId, Guid shareId, CancellationToken cancellationToken);

    Task<RepoBrowseDto> BrowseAsync(WorkspaceActor actor, Guid? folderId, Guid? documentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceChatDto>> ListFolderChatsAsync(WorkspaceActor actor, Guid folderId, CancellationToken cancellationToken);

    Task<RepoFolderDto> CreateFolderAsync(WorkspaceActor actor, Guid parentId, string name, CancellationToken cancellationToken);

    Task<RepoFolderDto> RenameFolderAsync(WorkspaceActor actor, Guid folderId, string name, CancellationToken cancellationToken);

    Task<RepoFolderDto> MoveFolderAsync(WorkspaceActor actor, Guid folderId, Guid parentId, CancellationToken cancellationToken);

    Task DeleteFolderAsync(WorkspaceActor actor, Guid folderId, bool recursive, CancellationToken cancellationToken);

    Task<RepoDocumentDto> RenameDocumentAsync(WorkspaceActor actor, Guid documentId, string name, CancellationToken cancellationToken);

    Task<RepoDocumentDto> MoveDocumentAsync(WorkspaceActor actor, Guid documentId, Guid folderId, CancellationToken cancellationToken);

    Task DeleteDocumentAsync(WorkspaceActor actor, Guid documentId, CancellationToken cancellationToken);

    Task<RepoDocumentDto> UploadAsync(WorkspaceActor actor, Guid folderId, string fileName, string contentType, Stream content, CancellationToken cancellationToken);

    Task<RepoDocumentDto> ReindexDocumentAsync(WorkspaceActor actor, Guid documentId, CancellationToken cancellationToken);

    Task<RepoSearchResponseDto> SearchAsync(WorkspaceActor actor, string query, Guid? folderId, IReadOnlyList<string> types, string? modified, CancellationToken cancellationToken);
}

public sealed partial class WorkspaceService : IWorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IntranetDbContext _db;
    private readonly IWorkspaceDirectory _directory;
    private readonly IRepositorySearch _search;
    private readonly IRepositoryReindexService _reindex;
    private readonly IWorkspaceChatComposer _composer;
    private readonly ChatCompletionRouter _llm;
    private readonly AgenticOptions _options;
    private readonly IWebHostEnvironment _environment;

    public WorkspaceService(
        IntranetDbContext db,
        IWorkspaceDirectory directory,
        IRepositorySearch search,
        IRepositoryReindexService reindex,
        IWorkspaceChatComposer composer,
        ChatCompletionRouter llm,
        IOptions<AgenticOptions> options,
        IWebHostEnvironment environment)
    {
        _db = db;
        _directory = directory;
        _search = search;
        _reindex = reindex;
        _composer = composer;
        _llm = llm;
        _options = options.Value;
        _environment = environment;
    }

    public WorkspaceCapabilitiesDto Capabilities() =>
        new(
            _options.Enabled,
            _options.AllowAnonymous,
            _options.UseGraphCalendar,
            _options.UseGraphMail,
            _options.SeedDemoData,
            _options.ShareDemoCatalog,
            _llm.IsFallbackConfigured,
            _directory.GraphConfigured,
            _search.Provider,
            "signalr",
            _llm.IsFallbackConfigured ? "hosted-or-ollama" : "search-fallback",
            RepositoryStorage.ResolveRoot(_options, _environment),
            _options.DemoUserOid,
            _options.DemoUserName);

    public async Task<WorkspaceSidebarDto> GetSidebarAsync(WorkspaceActor actor, CancellationToken cancellationToken)
    {
        var projects = await ListProjectsAsync(actor, cancellationToken);
        var chats = await VisibleChats(actor, cancellationToken);
        var repo = await _db.AgenticRepoFolders.AsNoTracking().OrderBy(f => f.Path).ToListAsync(cancellationToken);
        var childCounts = repo.GroupBy(f => f.ParentId).ToDictionary(g => g.Key ?? Guid.Empty, g => g.Count());
        var chatCounts = chats
            .Where(c => c.FolderId is not null)
            .GroupBy(c => c.FolderId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var owned = chats.Where(c => c.Role == AgenticRoles.Owner).ToList();
        return new WorkspaceSidebarDto(
            new WorkspaceMeDto(actor.Oid, actor.DisplayName, actor.Email),
            projects,
            owned.Where(c => c.ProjectId is null && c.FolderId is null && !c.Visibility.Equals("shared", StringComparison.Ordinal)).ToList(),
            chats.Where(c => c.Role != AgenticRoles.Owner).ToList(),
            owned.Where(c => c.Visibility.Equals("shared", StringComparison.Ordinal)).ToList(),
            owned.Where(c => c.ProjectId is not null || c.FolderId is not null).ToList(),
            repo.Select(f => ToFolderDto(f, childCounts.GetValueOrDefault(f.Id), chatCounts.GetValueOrDefault(f.Id))).ToList());
    }

    public async Task<IReadOnlyList<WorkspaceProjectDto>> ListProjectsAsync(WorkspaceActor actor, CancellationToken cancellationToken)
    {
        var projects = await _db.AgenticProjects
            .Include(p => p.Shares)
            .Include(p => p.Chats)
            .Include(p => p.Files)
            .ToListAsync(cancellationToken);
        var oid = EffectiveOid(actor);
        var groups = projects.SelectMany(p => p.Shares)
            .Where(s => s.PrincipalType == AgenticRoles.Group)
            .Select(s => s.PrincipalOid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var member = await _directory.CheckMemberGroupsAsync(oid, groups, cancellationToken);
        return projects
            .Select(p => (Project: p, Role: AgenticAccess.ResolveProjectRole(oid, p, member)))
            .Where(x => AgenticRoles.CanView(x.Role))
            .Select(x => ToProjectDto(x.Project, x.Role!))
            .OrderBy(p => p.Name)
            .ToList();
    }

    public async Task<WorkspaceProjectDto> GetProjectAsync(WorkspaceActor actor, Guid projectId, CancellationToken cancellationToken)
    {
        var (project, role) = await RequireProject(actor, projectId, view: true, cancellationToken: cancellationToken);
        return ToProjectDto(project, role);
    }

    public async Task<IReadOnlyList<RepoDocumentDto>> ListProjectFilesAsync(WorkspaceActor actor, Guid projectId, CancellationToken cancellationToken)
    {
        var (project, _) = await RequireProject(actor, projectId, view: true, cancellationToken: cancellationToken);
        return project.Files.Select(f => ToDocumentDto(f.Document, f.Document.Folder.Path)).ToList();
    }

    public async Task<IReadOnlyList<WorkspaceChatDto>> ListProjectChatsAsync(WorkspaceActor actor, Guid projectId, CancellationToken cancellationToken)
    {
        await RequireProject(actor, projectId, view: true, cancellationToken: cancellationToken);
        var chats = await VisibleChats(actor, cancellationToken);
        return chats.Where(c => c.ProjectId == projectId).ToList();
    }

    public async Task<WorkspaceProjectDto> CreateProjectAsync(WorkspaceActor actor, CreateProjectRequestDto request, CancellationToken cancellationToken)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0)
        {
            throw new AgenticException("Project name is required.", StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var project = new AgenticProject
        {
            Id = Guid.NewGuid(),
            OwnerOid = actor.Oid,
            Name = name,
            Instructions = request.Instructions?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.AgenticProjects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);
        return ToProjectDto(project, AgenticRoles.Owner);
    }

    public async Task<WorkspaceProjectDto> UpdateProjectAsync(WorkspaceActor actor, Guid projectId, UpdateProjectRequestDto request, CancellationToken cancellationToken)
    {
        var (project, role) = await RequireProject(actor, projectId, edit: true, cancellationToken: cancellationToken);
        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
            {
                throw new AgenticException("Project name is required.", StatusCodes.Status400BadRequest);
            }

            project.Name = name;
        }

        if (request.Instructions is not null)
        {
            project.Instructions = request.Instructions.Trim();
        }

        if (request.LinkedDocumentIds is not null)
        {
            _db.AgenticProjectFiles.RemoveRange(project.Files);
            foreach (var documentId in request.LinkedDocumentIds.Distinct())
            {
                if (!await _db.AgenticRepoDocuments.AnyAsync(d => d.Id == documentId, cancellationToken))
                {
                    continue;
                }

                _db.AgenticProjectFiles.Add(new AgenticProjectFile
                {
                    ProjectId = project.Id,
                    DocumentId = documentId,
                    AddedAt = DateTimeOffset.UtcNow,
                });
            }
        }

        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(project).Collection(p => p.Files).LoadAsync(cancellationToken);
        await _db.Entry(project).Collection(p => p.Chats).LoadAsync(cancellationToken);
        return ToProjectDto(project, role);
    }

    public async Task<WorkspaceChatDto> CreateChatAsync(WorkspaceActor actor, CreateChatRequestDto request, CancellationToken cancellationToken)
    {
        if (request.ProjectId is { } projectId)
        {
            await RequireProject(actor, projectId, view: true, cancellationToken: cancellationToken);
        }

        Guid? folderId = request.FolderId;
        Guid? documentId = request.DocumentId;
        if (documentId is { } attachId)
        {
            var attached = await _db.AgenticRepoDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == attachId, cancellationToken)
                ?? throw new AgenticException("File not found.", StatusCodes.Status404NotFound);
            folderId ??= attached.FolderId;
        }

        if (folderId is { } resolvedFolderId)
        {
            _ = await _db.AgenticRepoFolders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == resolvedFolderId, cancellationToken)
                ?? throw new AgenticException("Folder not found.", StatusCodes.Status404NotFound);
        }

        var now = DateTimeOffset.UtcNow;
        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = actor.Oid,
            ProjectId = request.ProjectId,
            FolderId = folderId,
            DocumentId = documentId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "New chat" : request.Title.Trim(),
            RealtimeCollaboration = request.RealtimeCollaboration,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.AgenticChats.Add(chat);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetChatAsync(actor, chat.Id, cancellationToken);
    }

    public async Task<WorkspaceChatDto> GetChatAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken)
    {
        var (chat, role) = await RequireChat(actor, chatId, view: true, cancellationToken: cancellationToken);
        return ToChatDto(chat, role, EffectiveOid(actor));
    }

    public const int ChatTitleMaxLength = 200;

    public async Task<WorkspaceChatDto> UpdateChatAsync(WorkspaceActor actor, Guid chatId, UpdateChatRequestDto request, CancellationToken cancellationToken)
    {
        var (chat, role) = await RequireChat(actor, chatId, edit: true, cancellationToken: cancellationToken);
        if (request.Title is not null)
        {
            if (!AgenticRoles.CanManage(role))
            {
                throw new AgenticException("Only the owner can rename this chat.", StatusCodes.Status403Forbidden);
            }

            chat.Title = NormalizeChatTitle(request.Title);
        }

        if (request.ProjectId.HasValue)
        {
            if (request.ProjectId.Value == Guid.Empty)
            {
                chat.ProjectId = null;
            }
            else
            {
                await RequireProject(actor, request.ProjectId.Value, view: true, cancellationToken: cancellationToken);
                chat.ProjectId = request.ProjectId;
            }
        }

        if (request.FolderId.HasValue)
        {
            if (request.FolderId.Value == Guid.Empty)
            {
                chat.FolderId = null;
            }
            else
            {
                _ = await _db.AgenticRepoFolders.AsNoTracking()
                        .FirstOrDefaultAsync(f => f.Id == request.FolderId.Value, cancellationToken)
                    ?? throw new AgenticException("Folder not found.", StatusCodes.Status404NotFound);
                chat.FolderId = request.FolderId;
            }
        }

        if (request.RealtimeCollaboration is { } realtime)
        {
            chat.RealtimeCollaboration = realtime;
        }

        if (request.DocumentId.HasValue)
        {
            if (request.DocumentId.Value == Guid.Empty)
            {
                chat.DocumentId = null;
            }
            else
            {
                var attached = await _db.AgenticRepoDocuments.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == request.DocumentId.Value, cancellationToken)
                    ?? throw new AgenticException("File not found.", StatusCodes.Status404NotFound);
                chat.DocumentId = attached.Id;
                chat.FolderId ??= attached.FolderId;
            }
        }

        chat.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToChatDto(chat, role, EffectiveOid(actor));
    }

    public async Task DeleteChatAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken)
    {
        var (chat, _) = await RequireChat(actor, chatId, manage: true, cancellationToken: cancellationToken);
        await _db.Entry(chat).Collection(c => c.Messages).LoadAsync(cancellationToken);
        _db.AgenticChatMessages.RemoveRange(chat.Messages);
        _db.AgenticChatShares.RemoveRange(chat.Shares);
        _db.AgenticChats.Remove(chat);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task LeaveChatAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken)
    {
        var (chat, role) = await RequireChat(actor, chatId, view: true, cancellationToken: cancellationToken);
        if (AgenticRoles.CanManage(role))
        {
            throw new AgenticException("Owners cannot leave their own chat. Delete it instead.", StatusCodes.Status400BadRequest);
        }

        var oid = EffectiveOid(actor);
        var share = chat.Shares.FirstOrDefault(s =>
                s.PrincipalType == AgenticRoles.User && AgenticAccess.OidsEqual(s.PrincipalOid, oid))
            ?? throw new AgenticException(
                "This chat is shared with a group. Ask the owner to remove access.",
                StatusCodes.Status400BadRequest);
        _db.AgenticChatShares.Remove(share);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceMessageDto>> ListMessagesAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken)
    {
        await RequireChat(actor, chatId, view: true, cancellationToken: cancellationToken);
        var messages = await _db.AgenticChatMessages
            .AsNoTracking()
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        return messages.Select(ToMessageDto).ToList();
    }

    public async Task<(WorkspaceMessageDto User, WorkspaceMessageDto Assistant)> SendAsync(
        WorkspaceActor actor,
        Guid chatId,
        string text,
        CancellationToken cancellationToken)
    {
        var (chat, role) = await RequireChat(actor, chatId, edit: true, cancellationToken: cancellationToken);
        if (!AgenticRoles.CanEdit(role))
        {
            throw new AgenticException("Viewers can read this chat but cannot send messages.", StatusCodes.Status403Forbidden);
        }

        var trimmed = (text ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new AgenticException("Message text is required.", StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var userMessage = new AgenticChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = "user",
            AuthorOid = actor.Oid,
            AuthorName = actor.DisplayName,
            Content = trimmed,
            CreatedAt = now,
        };
        _db.AgenticChatMessages.Add(userMessage);

        if (chat.Title == "New chat")
        {
            chat.Title = trimmed.Length <= 48 ? trimmed : trimmed[..48];
        }

        var history = await _db.AgenticChatMessages
            .AsNoTracking()
            .Where(m => m.ChatId == chat.Id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        var composed = await _composer.ComposeAsync(chat, trimmed, history, cancellationToken);
        var assistant = new AgenticChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = "assistant",
            AuthorOid = "agent",
            AuthorName = "Assistant",
            Content = composed.Content,
            CitationsJson = composed.Citations.Count == 0 ? null : JsonSerializer.Serialize(composed.Citations, JsonOptions),
            CreatedAt = now.AddMilliseconds(10),
        };
        _db.AgenticChatMessages.Add(assistant);
        chat.UpdatedAt = assistant.CreatedAt;
        await _db.SaveChangesAsync(cancellationToken);
        return (ToMessageDto(userMessage), ToMessageDto(assistant));
    }

    public async Task<IReadOnlyList<ChatShareDto>> ListSharesAsync(WorkspaceActor actor, Guid chatId, CancellationToken cancellationToken)
    {
        var (chat, _) = await RequireChat(actor, chatId, view: true, cancellationToken: cancellationToken);
        return chat.Shares.OrderBy(s => s.PrincipalDisplayName).Select(ToShareDto).ToList();
    }

    public async Task<ChatShareDto> AddShareAsync(WorkspaceActor actor, Guid chatId, CreateShareRequestDto request, CancellationToken cancellationToken)
    {
        var (chat, role) = await RequireChat(actor, chatId, manage: true, cancellationToken: cancellationToken);
        if (!AgenticRoles.CanManage(role))
        {
            throw new AgenticException("Only the owner can share this chat.", StatusCodes.Status403Forbidden);
        }

        var principalType = AgenticAccess.NormalizePrincipalType(request.PrincipalType);
        var shareRole = AgenticAccess.NormalizeRole(request.Role);
        var oid = (request.PrincipalOid ?? "").Trim();
        if (oid.Length == 0)
        {
            throw new AgenticException("principalOid is required.", StatusCodes.Status400BadRequest);
        }

        if (chat.Shares.Any(s =>
                s.PrincipalType == principalType && AgenticAccess.OidsEqual(s.PrincipalOid, oid)))
        {
            throw new AgenticException("That person or group already has access.", StatusCodes.Status409Conflict);
        }

        var share = new AgenticChatShare
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            PrincipalType = principalType,
            PrincipalOid = oid,
            PrincipalDisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? oid : request.DisplayName.Trim(),
            PrincipalEmail = request.Email,
            Role = shareRole,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByOid = actor.Oid,
        };
        _db.AgenticChatShares.Add(share);
        chat.UpdatedAt = share.CreatedAt;
        await _db.SaveChangesAsync(cancellationToken);
        return ToShareDto(share);
    }

    public async Task RemoveShareAsync(WorkspaceActor actor, Guid chatId, Guid shareId, CancellationToken cancellationToken)
    {
        var (chat, role) = await RequireChat(actor, chatId, manage: true, cancellationToken: cancellationToken);
        if (!AgenticRoles.CanManage(role))
        {
            throw new AgenticException("Only the owner can change sharing.", StatusCodes.Status403Forbidden);
        }

        var share = chat.Shares.FirstOrDefault(s => s.Id == shareId)
            ?? throw new AgenticException("Share not found.", StatusCodes.Status404NotFound);
        _db.AgenticChatShares.Remove(share);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RepoBrowseDto> BrowseAsync(WorkspaceActor actor, Guid? folderId, Guid? documentId, CancellationToken cancellationToken)
    {
        _ = actor;
        var folders = await _db.AgenticRepoFolders.AsNoTracking().ToListAsync(cancellationToken);
        var selectedFolderId = folderId
            ?? (documentId is { } docId
                ? (await _db.AgenticRepoDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId, cancellationToken))?.FolderId
                : null)
            ?? WorkspaceIds.FolderRoot;
        var folder = folders.FirstOrDefault(f => f.Id == selectedFolderId)
            ?? folders.FirstOrDefault(f => f.ParentId is null);
        if (folder is null)
        {
            await EnsureRootFolderAsync(cancellationToken);
            folders = await _db.AgenticRepoFolders.AsNoTracking().ToListAsync(cancellationToken);
            folder = folders.FirstOrDefault(f => f.ParentId is null)
                ?? throw new AgenticException("Repository is empty.", StatusCodes.Status404NotFound);
            selectedFolderId = folder.Id;
        }

        var childCounts = folders.GroupBy(f => f.ParentId).ToDictionary(g => g.Key ?? Guid.Empty, g => g.Count());
        var chatCounts = await ChatCountsByFolderAsync(cancellationToken);
        var children = folders
            .Where(f => f.ParentId == folder.Id)
            .OrderBy(f => f.Name)
            .Select(f => ToFolderDto(f, childCounts.GetValueOrDefault(f.Id), chatCounts.GetValueOrDefault(f.Id)))
            .ToList();
        var documents = await _db.AgenticRepoDocuments
            .AsNoTracking()
            .Include(d => d.Folder)
            .Where(d => d.FolderId == folder.Id)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
        var selected = documentId is { } id ? documents.FirstOrDefault(d => d.Id == id) : documents.FirstOrDefault();
        return new RepoBrowseDto(
            ToFolderDto(folder, childCounts.GetValueOrDefault(folder.Id), chatCounts.GetValueOrDefault(folder.Id)),
            BuildBreadcrumbs(folder, folders, childCounts, chatCounts),
            children,
            documents.Select(d => ToDocumentDto(d, d.Folder.Path)).ToList(),
            selected is null ? null : ToDocumentDto(selected, selected.Folder.Path));
    }

    public async Task<RepoDocumentDto> UploadAsync(
        WorkspaceActor actor,
        Guid folderId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        RequireSignedIn(actor);
        var folder = await _db.AgenticRepoFolders.FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken)
            ?? throw new AgenticException("Folder not found.", StatusCodes.Status404NotFound);
        var safeName = RepositoryNames.NormalizeFileName(fileName);
        await EnsureUniqueDocumentNameAsync(folder.Id, safeName, excludeId: null, cancellationToken);

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var extracted = RepositoryTextExtractor.Extract(safeName, contentType, bytes);
        buffer.Position = 0;
        var id = Guid.NewGuid();
        var root = RepositoryStorage.ResolveRoot(_options, _environment);
        var folderDir = RepositoryStorage.FolderDirectory(root, folder);
        Directory.CreateDirectory(folderDir);
        var storedName = $"{id:N}{Path.GetExtension(safeName)}";
        var full = Path.Combine(folderDir, storedName);
        await using (var file = File.Create(full))
        {
            await buffer.CopyToAsync(file, cancellationToken);
        }

        var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
        var document = new AgenticRepoDocument
        {
            Id = id,
            FolderId = folder.Id,
            FolderPath = folder.Path,
            Name = safeName,
            FileType = RepositoryTextExtractor.DetectFileType(safeName, contentType),
            MimeType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = buffer.Length,
            OwnerName = actor.DisplayName,
            CreatedByOid = actor.Oid,
            UpdatedByOid = actor.Oid,
            UpdatedByName = actor.DisplayName,
            StoragePath = relative,
            ExtractedText = string.IsNullOrWhiteSpace(extracted.Text) ? null : extracted.Text,
            ExtractionStatus = extracted.Status,
            ExtractionNote = extracted.Note,
            IsIndexed = false,
            ModifiedAt = DateTimeOffset.UtcNow,
            Folder = folder,
        };
        _db.AgenticRepoDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);
        await _search.ReindexDocumentAsync(document, cancellationToken);
        return ToDocumentDto(document, folder.Path);
    }

    public async Task<RepoDocumentDto> ReindexDocumentAsync(
        WorkspaceActor actor,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        RequireSignedIn(actor);
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        await _reindex.ReextractDocumentAsync(document, cancellationToken);
        return ToDocumentDto(document, document.Folder.Path);
    }

    public async Task<RepoSearchResponseDto> SearchAsync(
        WorkspaceActor actor,
        string query,
        Guid? folderId,
        IReadOnlyList<string> types,
        string? modified,
        CancellationToken cancellationToken)
    {
        _ = actor;
        var typeSet = types
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t is "document" or "spreadsheet" or "image")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? folderPath = null;
        if (folderId is { } scopeId)
        {
            folderPath = await _db.AgenticRepoFolders.AsNoTracking()
                .Where(f => f.Id == scopeId)
                .Select(f => f.Path)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var hits = await _search.SearchAsync(new RepoSearchQuery(query, folderId, typeSet, modified), cancellationToken);
        return new RepoSearchResponseDto(
            query,
            folderId,
            folderPath,
            folderId is not null,
            hits.Count,
            hits.Select(h => new RepoSearchHitDto(h.DocumentId, h.FolderId, h.Title, h.FileType, h.Path, h.Snippet, h.Highlights)).ToList());
    }

    private async Task<List<WorkspaceChatDto>> VisibleChats(WorkspaceActor actor, CancellationToken cancellationToken)
    {
        var chats = await _db.AgenticChats
            .Include(c => c.Shares)
            .Include(c => c.Folder)
            .Include(c => c.Document)
            .Include(c => c.Project)!.ThenInclude(p => p!.Shares)
            .ToListAsync(cancellationToken);
        var oid = EffectiveOid(actor);
        var groupOids = chats
            .SelectMany(c => c.Shares.Select(s => (s.PrincipalType, s.PrincipalOid)))
            .Concat(chats.SelectMany(c => (c.Project?.Shares ?? []).Select(s => (s.PrincipalType, s.PrincipalOid))))
            .Where(s => s.PrincipalType == AgenticRoles.Group)
            .Select(s => s.PrincipalOid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var member = await _directory.CheckMemberGroupsAsync(oid, groupOids, cancellationToken);
        return chats
            .Select(c => (Chat: c, Role: AgenticAccess.ResolveChatRole(oid, c, member)))
            .Where(x => AgenticRoles.CanView(x.Role))
            .Select(x => ToChatDto(x.Chat, x.Role!, oid))
            .OrderByDescending(c => c.UpdatedAt)
            .ToList();
    }

    private async Task<(AgenticChat Chat, string Role)> RequireChat(
        WorkspaceActor actor,
        Guid chatId,
        bool view = false,
        bool edit = false,
        bool manage = false,
        CancellationToken cancellationToken = default)
    {
        var chat = await _db.AgenticChats
            .Include(c => c.Shares)
            .Include(c => c.Folder)
            .Include(c => c.Document)
            .Include(c => c.Project)!.ThenInclude(p => p!.Shares)
            .Include(c => c.Project)!.ThenInclude(p => p!.Files).ThenInclude(f => f.Document).ThenInclude(d => d.Folder)
            .FirstOrDefaultAsync(c => c.Id == chatId, cancellationToken)
            ?? throw new AgenticException("Chat not found.", StatusCodes.Status404NotFound);
        var oid = EffectiveOid(actor);
        var groups = chat.Shares
            .Where(s => s.PrincipalType == AgenticRoles.Group)
            .Select(s => s.PrincipalOid)
            .Concat(chat.Project?.Shares.Where(s => s.PrincipalType == AgenticRoles.Group).Select(s => s.PrincipalOid) ?? [])
            .ToList();
        var member = await _directory.CheckMemberGroupsAsync(oid, groups, cancellationToken);
        var role = AgenticAccess.ResolveChatRole(oid, chat, member);
        if (role is null)
        {
            throw new AgenticException("Chat not found.", StatusCodes.Status404NotFound);
        }

        if ((manage && !AgenticRoles.CanManage(role))
            || (edit && !AgenticRoles.CanEdit(role))
            || (view && !AgenticRoles.CanView(role)))
        {
            throw new AgenticException("You do not have permission for that action.", StatusCodes.Status403Forbidden);
        }

        return (chat, role);
    }

    private async Task<(AgenticProject Project, string Role)> RequireProject(
        WorkspaceActor actor,
        Guid projectId,
        bool view = false,
        bool edit = false,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.AgenticProjects
            .Include(p => p.Shares)
            .Include(p => p.Chats)
            .Include(p => p.Files).ThenInclude(f => f.Document).ThenInclude(d => d.Folder)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new AgenticException("Project not found.", StatusCodes.Status404NotFound);
        var oid = EffectiveOid(actor);
        var groups = project.Shares.Where(s => s.PrincipalType == AgenticRoles.Group).Select(s => s.PrincipalOid).ToList();
        var member = await _directory.CheckMemberGroupsAsync(oid, groups, cancellationToken);
        var role = AgenticAccess.ResolveProjectRole(oid, project, member);
        if (role is null)
        {
            throw new AgenticException("Project not found.", StatusCodes.Status404NotFound);
        }

        if ((edit && !AgenticRoles.CanEdit(role)) || (view && !AgenticRoles.CanView(role)))
        {
            throw new AgenticException("You do not have permission for that action.", StatusCodes.Status403Forbidden);
        }

        return (project, role);
    }

    private async Task EnsureRootFolderAsync(CancellationToken cancellationToken)
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
    }

    private string EffectiveOid(WorkspaceActor actor) =>
        _options.ShareDemoCatalog && !AgenticAccess.OidsEqual(actor.Oid, WorkspaceIds.DemoYouOid)
            ? WorkspaceIds.DemoYouOid
            : actor.Oid;

    private static WorkspaceProjectDto ToProjectDto(AgenticProject project, string role) =>
        new(project.Id, project.Name, project.Instructions, role, project.Chats.Count, project.Files.Count, project.UpdatedAt);

    private static WorkspaceChatDto ToChatDto(AgenticChat chat, string role, string userOid)
    {
        var shareCount = chat.Shares.Count;
        var visibility = shareCount > 0 || chat.RealtimeCollaboration ? "shared" : "private";
        var myShareId = chat.Shares
            .FirstOrDefault(s => s.PrincipalType == AgenticRoles.User && AgenticAccess.OidsEqual(s.PrincipalOid, userOid))
            ?.Id;
        return new WorkspaceChatDto(
            chat.Id,
            chat.ProjectId,
            chat.Project?.Name,
            chat.FolderId,
            chat.Folder?.Name,
            chat.Folder?.Path,
            chat.DocumentId,
            chat.Document?.Name,
            chat.Title,
            visibility,
            chat.RealtimeCollaboration,
            role,
            shareCount,
            chat.UpdatedAt,
            Relative(chat.UpdatedAt),
            myShareId);
    }

    private static string NormalizeChatTitle(string title)
    {
        var trimmed = title.Trim();
        if (trimmed.Length == 0)
        {
            throw new AgenticException("Title is required.", StatusCodes.Status400BadRequest);
        }

        if (trimmed.Length > ChatTitleMaxLength)
        {
            throw new AgenticException(
                $"Title must be {ChatTitleMaxLength} characters or fewer.",
                StatusCodes.Status400BadRequest);
        }

        return trimmed;
    }

    private static WorkspaceMessageDto ToMessageDto(AgenticChatMessage message) =>
        new(
            message.Id,
            message.ChatId,
            message.Role,
            message.AuthorOid,
            message.AuthorName,
            message.Content,
            ParseCitations(message.CitationsJson),
            message.CreatedAt);

    private static ChatShareDto ToShareDto(AgenticChatShare share) =>
        new(share.Id, share.PrincipalType, share.PrincipalOid, share.PrincipalDisplayName, share.PrincipalEmail, share.Role, share.CreatedAt);

    private static RepoFolderDto ToFolderDto(AgenticRepoFolder folder, int childCount, int chatCount = 0) =>
        new(
            folder.Id,
            folder.ParentId,
            folder.Name,
            folder.Path,
            folder.IsPublic,
            folder.IsIndexed,
            childCount,
            chatCount,
            string.IsNullOrWhiteSpace(folder.CreatedByName) ? null : folder.CreatedByName,
            folder.UpdatedAt == default ? folder.CreatedAt : folder.UpdatedAt);

    internal static void RequireSignedIn(WorkspaceActor actor)
    {
        if (string.IsNullOrWhiteSpace(actor.Oid) || actor.Oid.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            throw new AgenticException("Sign in to change the repository.", StatusCodes.Status401Unauthorized);
        }
    }

    private static RepoDocumentDto ToDocumentDto(AgenticRepoDocument document, string folderPath) =>
        new(
            document.Id,
            document.FolderId,
            document.Name,
            document.FileType,
            document.MimeType,
            document.SizeBytes,
            document.OwnerName,
            document.IsIndexed,
            true,
            folderPath.TrimEnd('/') + " / " + document.Name,
            document.ModifiedAt,
            Trim(document.ExtractedText, 400),
            string.IsNullOrWhiteSpace(document.ExtractionStatus) ? RepositoryExtractionStatus.Pending : document.ExtractionStatus,
            document.ExtractionNote);

    private async Task<Dictionary<Guid, int>> ChatCountsByFolderAsync(CancellationToken cancellationToken)
    {
        return await _db.AgenticChats.AsNoTracking()
            .Where(c => c.FolderId != null)
            .GroupBy(c => c.FolderId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
    }

    private static List<RepoFolderDto> BuildBreadcrumbs(
        AgenticRepoFolder folder,
        IReadOnlyList<AgenticRepoFolder> all,
        IReadOnlyDictionary<Guid, int> childCounts,
        IReadOnlyDictionary<Guid, int>? chatCounts = null)
    {
        var map = all.ToDictionary(f => f.Id);
        var stack = new List<AgenticRepoFolder>();
        var current = folder;
        while (true)
        {
            stack.Add(current);
            if (current.ParentId is not { } parentId || !map.TryGetValue(parentId, out var parent))
            {
                break;
            }

            current = parent;
        }

        stack.Reverse();
        return stack.Select(f => ToFolderDto(f, childCounts.GetValueOrDefault(f.Id), chatCounts?.GetValueOrDefault(f.Id) ?? 0)).ToList();
    }

    private static IReadOnlyList<WorkspaceCitationDto> ParseCitations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<WorkspaceCitationDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Relative(DateTimeOffset when)
    {
        var delta = DateTimeOffset.UtcNow - when;
        if (delta.TotalMinutes < 1)
        {
            return "just now";
        }

        if (delta.TotalHours < 1)
        {
            return $"{(int)delta.TotalMinutes}m ago";
        }

        if (delta.TotalDays < 1)
        {
            return $"{(int)delta.TotalHours}h ago";
        }

        return when.ToString("MMM d");
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? value : value.Length <= max ? value : value[..max] + "…";

}

public sealed class AgenticException : Exception
{
    public int StatusCode { get; }

    public AgenticException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
