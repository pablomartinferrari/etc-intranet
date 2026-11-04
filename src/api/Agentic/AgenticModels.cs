namespace Intranet.Api.Agentic;

public sealed record WorkspaceCapabilitiesDto(
    bool Enabled,
    bool AllowAnonymous,
    bool UseGraphCalendar,
    string SearchProvider,
    string RealtimeProvider,
    string DemoUserOid,
    string DemoUserName);

public sealed record WorkspaceMeDto(string Oid, string DisplayName, string? Email);

public sealed record DayItemDto(
    string Id,
    string Title,
    string? Detail,
    DateTimeOffset? When,
    string? Href);

public sealed record DaySectionDto(string Id, string Title, IReadOnlyList<DayItemDto> Items);

public sealed record DaySummaryDto(
    string Greeting,
    string UserDisplayName,
    DateOnly Date,
    IReadOnlyList<DaySectionDto> Sections,
    IReadOnlyList<string> Sources);

public sealed record WorkspaceProjectDto(
    Guid Id,
    string Name,
    string? Instructions,
    string Role,
    int ChatCount,
    int FileCount,
    DateTimeOffset UpdatedAt);

public sealed record WorkspaceChatDto(
    Guid Id,
    Guid? ProjectId,
    string? ProjectName,
    string Title,
    string Visibility,
    bool RealtimeCollaboration,
    string Role,
    int ShareCount,
    DateTimeOffset UpdatedAt,
    string? LastActivityLabel);

public sealed record WorkspaceSidebarDto(
    WorkspaceMeDto Me,
    IReadOnlyList<WorkspaceProjectDto> Projects,
    IReadOnlyList<WorkspaceChatDto> MyChats,
    IReadOnlyList<WorkspaceChatDto> SharedWithMe,
    IReadOnlyList<WorkspaceChatDto> SharedByMe,
    IReadOnlyList<RepoFolderDto> Repository);

public sealed record WorkspaceCitationDto(
    string Type,
    string Title,
    string Snippet,
    Guid? DocumentId,
    string? Path,
    string? Url);

public sealed record WorkspaceMessageDto(
    Guid Id,
    Guid ChatId,
    string Role,
    string AuthorOid,
    string AuthorName,
    string Content,
    IReadOnlyList<WorkspaceCitationDto> Citations,
    DateTimeOffset CreatedAt);

public sealed record SendChatRequestDto(string Text);

public sealed record CreateChatRequestDto(string? Title, Guid? ProjectId, bool RealtimeCollaboration);

public sealed record UpdateChatRequestDto(string? Title, Guid? ProjectId, bool? RealtimeCollaboration);

public sealed record CreateProjectRequestDto(string Name, string? Instructions);

public sealed record UpdateProjectRequestDto(string? Name, string? Instructions, IReadOnlyList<Guid>? LinkedDocumentIds);

public sealed record DirectoryPrincipalDto(string Type, string Oid, string DisplayName, string? Email, int? MemberCount);

public sealed record ChatShareDto(
    Guid Id,
    string PrincipalType,
    string PrincipalOid,
    string PrincipalDisplayName,
    string? PrincipalEmail,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record CreateShareRequestDto(
    string PrincipalType,
    string PrincipalOid,
    string Role,
    string? DisplayName,
    string? Email);

public sealed record RepoFolderDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Path,
    bool IsPublic,
    bool IsIndexed,
    int ChildCount);

public sealed record RepoDocumentDto(
    Guid Id,
    Guid FolderId,
    string Name,
    string FileType,
    string MimeType,
    long SizeBytes,
    string OwnerName,
    bool IsIndexed,
    bool IsPublic,
    string Path,
    DateTimeOffset ModifiedAt,
    string? PreviewText);

public sealed record RepoBrowseDto(
    RepoFolderDto Folder,
    IReadOnlyList<RepoFolderDto> Breadcrumbs,
    IReadOnlyList<RepoFolderDto> Children,
    IReadOnlyList<RepoDocumentDto> Documents,
    RepoDocumentDto? Selected);

public sealed record RepoSearchHitDto(
    Guid DocumentId,
    string Title,
    string FileType,
    string Path,
    string Snippet,
    IReadOnlyList<string> Highlights);

public sealed record RepoSearchResponseDto(
    string Query,
    Guid? FolderId,
    int Total,
    IReadOnlyList<RepoSearchHitDto> Hits);

public sealed record PresenceUserDto(string Oid, string DisplayName, string Initial, bool Typing);
