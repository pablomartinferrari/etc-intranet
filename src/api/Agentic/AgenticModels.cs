namespace Intranet.Api.Agentic;

public sealed record WorkspaceCapabilitiesDto(
    bool Enabled,
    bool AllowAnonymous,
    bool UseGraphCalendar,
    bool UseGraphMail,
    bool SeedDemoData,
    bool ShareDemoCatalog,
    bool HostedLlmConfigured,
    bool DirectorySearchConfigured,
    string SearchProvider,
    string RealtimeProvider,
    string ChatModel,
    string RepositoryRoot,
    string DemoUserOid,
    string DemoUserName);

public sealed record WorkspaceMeDto(string Oid, string DisplayName, string? Email);

public sealed record DayItemDto(
    string Id,
    string Title,
    string? Detail,
    DateTimeOffset? When,
    string? Href,
    DateTimeOffset? End,
    string? Organizer,
    IReadOnlyList<string>? Attendees,
    string? Location,
    string? TeamsLink,
    string? BodyPreview);

public sealed record DaySectionDto(string Id, string Title, IReadOnlyList<DayItemDto> Items);

public sealed record MeetingBriefDto(
    string Text,
    string Source,
    IReadOnlyList<string> Highlights);

public sealed record CalendarStatusDto(string State, string? Message);

public sealed record MailStatusDto(string State, string? Message);

public sealed record DaySummaryDto(
    string Greeting,
    string UserDisplayName,
    DateOnly Date,
    string TimeZone,
    IReadOnlyList<DaySectionDto> Sections,
    IReadOnlyList<string> Sources,
    MeetingBriefDto? Brief,
    CalendarStatusDto Calendar,
    MailStatusDto Mail);

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
    Guid? FolderId,
    string? FolderName,
    string? FolderPath,
    Guid? DocumentId,
    string? DocumentName,
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
    IReadOnlyList<WorkspaceChatDto> ScopedChats,
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

public sealed record CreateChatRequestDto(
    string? Title,
    Guid? ProjectId,
    Guid? FolderId,
    bool RealtimeCollaboration,
    Guid? DocumentId = null);

public sealed record UpdateChatRequestDto(
    string? Title,
    Guid? ProjectId,
    Guid? FolderId,
    bool? RealtimeCollaboration,
    Guid? DocumentId = null);

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
    int ChildCount,
    int ChatCount = 0,
    string? CreatedByName = null,
    DateTimeOffset? UpdatedAt = null);

public sealed record CreateFolderRequestDto(string Name);

public sealed record RenameRepoItemRequestDto(string Name);

public sealed record MoveRepoItemRequestDto(Guid ParentId);

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
    string? PreviewText,
    string ExtractionStatus = "",
    string? ExtractionNote = null);

public sealed record RepoBrowseDto(
    RepoFolderDto Folder,
    IReadOnlyList<RepoFolderDto> Breadcrumbs,
    IReadOnlyList<RepoFolderDto> Children,
    IReadOnlyList<RepoDocumentDto> Documents,
    RepoDocumentDto? Selected);

public sealed record RepoSearchHitDto(
    Guid DocumentId,
    Guid FolderId,
    string Title,
    string FileType,
    string Path,
    string Snippet,
    IReadOnlyList<string> Highlights);

public sealed record RepoSearchResponseDto(
    string Query,
    Guid? FolderId,
    string? FolderPath,
    bool Scoped,
    int Total,
    IReadOnlyList<RepoSearchHitDto> Hits);

public sealed record PresenceUserDto(string Oid, string DisplayName, string Initial, bool Typing);
