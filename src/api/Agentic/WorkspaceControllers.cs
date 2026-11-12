using Intranet.Api.Agentic.Day;
using Intranet.Api.Agentic.Realtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Agentic;

[ApiController]
[Route("api/workspace")]
public abstract class WorkspaceControllerBase : ControllerBase
{
    private readonly AgenticOptions _options;

    protected WorkspaceControllerBase(IOptions<AgenticOptions> options)
    {
        _options = options.Value;
    }

    protected IActionResult? GuardEnabled()
    {
        if (_options.Enabled)
        {
            return null;
        }

        return NotFound(new { error = "agentic_workspace_disabled" });
    }

    protected WorkspaceActor Actor() => WorkspaceActor.FromUser(User, _options);

    protected IActionResult MapError(AgenticException ex) =>
        StatusCode(ex.StatusCode, new { error = "agentic_error", message = ex.Message });
}

public sealed class WorkspaceCapabilitiesController(IWorkspaceService workspace, IOptions<AgenticOptions> options)
    : WorkspaceControllerBase(options)
{
    [HttpGet("capabilities")]
    public IActionResult Get()
    {
        var disabled = GuardEnabled();
        return disabled ?? Ok(workspace.Capabilities());
    }
}

public sealed class WorkspaceDayController(
    IDayAggregator day,
    IOptions<AgenticOptions> options) : WorkspaceControllerBase(options)
{
    [HttpGet("day")]
    public async Task<IActionResult> Get(
        [FromHeader(Name = DayTimeZones.HeaderName)] string? timeZone,
        CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        var tz = DayTimeZones.Normalize(timeZone);
        var now = DayTimeZones.Now(tz);
        var date = DateOnly.FromDateTime(now.DateTime);
        var summary = await day.GetAsync(new DayQuery(Actor(), date, tz, now), cancellationToken);
        return Ok(summary);
    }
}

public sealed class WorkspaceCatalogController(
    IWorkspaceService workspace,
    IWorkspaceDirectory directory,
    IOptions<AgenticOptions> options) : WorkspaceControllerBase(options)
{
    [HttpGet("sidebar")]
    public async Task<IActionResult> Sidebar(CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        return Ok(await workspace.GetSidebarAsync(Actor(), cancellationToken));
    }

    [HttpGet("directory/search")]
    public async Task<IActionResult> Directory([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await directory.SearchAsync(q ?? "", cancellationToken));
        }
        catch (Intranet.Api.KnowledgeBase.Services.GraphDirectoryException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                error = "directory_search_failed",
                message = ex.Message,
                graphErrorCode = ex.GraphErrorCode,
            });
        }
    }

    [HttpGet("projects")]
    public async Task<IActionResult> Projects(CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        return Ok(await workspace.ListProjectsAsync(Actor(), cancellationToken));
    }

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequestDto body, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.CreateProjectAsync(Actor(), body, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<IActionResult> GetProject(Guid projectId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.GetProjectAsync(Actor(), projectId, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPatch("projects/{projectId:guid}")]
    public async Task<IActionResult> UpdateProject(Guid projectId, [FromBody] UpdateProjectRequestDto body, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.UpdateProjectAsync(Actor(), projectId, body, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("projects/{projectId:guid}/files")]
    public async Task<IActionResult> ProjectFiles(Guid projectId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.ListProjectFilesAsync(Actor(), projectId, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("projects/{projectId:guid}/chats")]
    public async Task<IActionResult> ProjectChats(Guid projectId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.ListProjectChatsAsync(Actor(), projectId, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPost("chats")]
    public async Task<IActionResult> CreateChat([FromBody] CreateChatRequestDto body, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.CreateChatAsync(Actor(), body, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("chats/{chatId:guid}")]
    public async Task<IActionResult> GetChat(Guid chatId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.GetChatAsync(Actor(), chatId, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPatch("chats/{chatId:guid}")]
    public async Task<IActionResult> UpdateChat(Guid chatId, [FromBody] UpdateChatRequestDto body, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.UpdateChatAsync(Actor(), chatId, body, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("chats/{chatId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid chatId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.ListMessagesAsync(Actor(), chatId, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("chats/{chatId:guid}/shares")]
    public async Task<IActionResult> Shares(Guid chatId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.ListSharesAsync(Actor(), chatId, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPost("chats/{chatId:guid}/shares")]
    public async Task<IActionResult> AddShare(Guid chatId, [FromBody] CreateShareRequestDto body, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.AddShareAsync(Actor(), chatId, body, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpDelete("chats/{chatId:guid}/shares/{shareId:guid}")]
    public async Task<IActionResult> RemoveShare(Guid chatId, Guid shareId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            await workspace.RemoveShareAsync(Actor(), chatId, shareId, cancellationToken);
            return NoContent();
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }
}

public sealed class WorkspaceChatSendController(
    IWorkspaceService workspace,
    IHubContext<WorkspaceHub> hub,
    IOptions<AgenticOptions> options) : WorkspaceControllerBase(options)
{
    [HttpPost("chats/{chatId:guid}/messages")]
    public async Task<IActionResult> Send(Guid chatId, [FromBody] SendChatRequestDto body, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            await hub.Clients.Group(WorkspaceHub.GroupName(chatId)).SendAsync("agent", new { status = "started" }, cancellationToken);
            var (user, assistant) = await workspace.SendAsync(Actor(), chatId, body.Text, cancellationToken);
            await hub.Clients.Group(WorkspaceHub.GroupName(chatId)).SendAsync("message", user, cancellationToken);
            await hub.Clients.Group(WorkspaceHub.GroupName(chatId)).SendAsync("message", assistant, cancellationToken);
            await hub.Clients.Group(WorkspaceHub.GroupName(chatId)).SendAsync("agent", new { status = "completed" }, cancellationToken);
            return Ok(new { user, assistant });
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }
}

public sealed class WorkspaceRepositoryController(
    IWorkspaceService workspace,
    IHubContext<WorkspaceHub> hub,
    IOptions<AgenticOptions> options) : WorkspaceControllerBase(options)
{
    [HttpGet("repo")]
    public async Task<IActionResult> Browse([FromQuery] Guid? folderId, [FromQuery] Guid? documentId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.BrowseAsync(Actor(), folderId, documentId, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("repo/{folderId:guid}/chats")]
    public async Task<IActionResult> FolderChats(Guid folderId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            return Ok(await workspace.ListFolderChatsAsync(Actor(), folderId, cancellationToken));
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPost("repo/{parentId:guid}/folders")]
    public async Task<IActionResult> CreateFolder(Guid parentId, [FromBody] CreateFolderRequestDto body, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            var folder = await workspace.CreateFolderAsync(Actor(), parentId, body.Name, cancellationToken);
            await NotifyRepositoryChanged(cancellationToken);
            return Ok(folder);
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPatch("repo/folders/{folderId:guid}")]
    public async Task<IActionResult> UpdateFolder(
        Guid folderId,
        [FromBody] PatchRepoItemRequestDto? patch,
        [FromQuery] Guid? parentId,
        CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            RepoFolderDto folder;
            var destination = parentId ?? patch?.ParentId;
            if (destination is { } target)
            {
                folder = await workspace.MoveFolderAsync(Actor(), folderId, target, cancellationToken);
            }
            else
            {
                folder = await workspace.RenameFolderAsync(Actor(), folderId, patch?.Name ?? "", cancellationToken);
            }

            await NotifyRepositoryChanged(cancellationToken);
            return Ok(folder);
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpDelete("repo/folders/{folderId:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid folderId, [FromQuery] bool recursive = false, CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            await workspace.DeleteFolderAsync(Actor(), folderId, recursive, cancellationToken);
            await NotifyRepositoryChanged(cancellationToken);
            return NoContent();
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPatch("repo/documents/{documentId:guid}")]
    public async Task<IActionResult> UpdateDocument(
        Guid documentId,
        [FromBody] PatchRepoItemRequestDto? patch,
        [FromQuery] Guid? folderId,
        CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            RepoDocumentDto document;
            var destination = folderId ?? patch?.FolderId;
            if (destination is { } target)
            {
                document = await workspace.MoveDocumentAsync(Actor(), documentId, target, cancellationToken);
            }
            else
            {
                document = await workspace.RenameDocumentAsync(Actor(), documentId, patch?.Name ?? "", cancellationToken);
            }

            await NotifyRepositoryChanged(cancellationToken);
            return Ok(document);
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpDelete("repo/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            await workspace.DeleteDocumentAsync(Actor(), documentId, cancellationToken);
            await NotifyRepositoryChanged(cancellationToken);
            return NoContent();
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPost("repo/documents/{documentId:guid}/reindex")]
    public async Task<IActionResult> ReindexDocument(Guid documentId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        try
        {
            var document = await workspace.ReindexDocumentAsync(Actor(), documentId, cancellationToken);
            await NotifyRepositoryChanged(cancellationToken);
            return Ok(document);
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPost("repo/{folderId:guid}/upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(Guid folderId, IFormFile file, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "missing_file", message = "Choose a file to upload." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var document = await workspace.UploadAsync(Actor(), folderId, file.FileName, file.ContentType ?? "application/octet-stream", stream, cancellationToken);
            await NotifyRepositoryChanged(cancellationToken);
            return Ok(document);
        }
        catch (AgenticException ex)
        {
            return MapError(ex);
        }
    }

    private Task NotifyRepositoryChanged(CancellationToken cancellationToken) =>
        hub.Clients.Group(WorkspaceHub.RepositoryGroup).SendAsync(
            "repositoryChanged",
            new { at = DateTimeOffset.UtcNow },
            cancellationToken);

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] Guid? folderId,
        [FromQuery] string? types,
        [FromQuery] string? modified,
        CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled)
        {
            return disabled;
        }

        var typeList = (types ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Ok(await workspace.SearchAsync(Actor(), q ?? "", folderId, typeList, modified, cancellationToken));
    }
}
