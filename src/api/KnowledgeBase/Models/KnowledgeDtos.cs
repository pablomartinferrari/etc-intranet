namespace Intranet.Api.KnowledgeBase.Models;

public sealed record SearchRequestDto(
    string Query,
    string? DocType = null,
    int Limit = 20);

public sealed record SearchResultItemDto(
    Guid DocumentId,
    string Title,
    string? SourceUri,
    string? DocType,
    string SourceType,
    double Score,
    string Snippet);

public sealed record SearchResponseDto(
    string Mode,
    IReadOnlyList<SearchResultItemDto> Results);

public sealed record ChatRequestDto(
    string Query,
    Guid? SessionId = null,
    Guid? DocumentId = null,
    Guid? ProjectId = null,
    string SearchMode = "auto");

public sealed record CitationDto(
    string Type,
    string Title,
    string Snippet,
    Guid? DocumentId = null,
    string? SourceUri = null,
    string? Url = null);

public sealed record ChatGenerationDto(
    string Provider,
    string Model,
    bool IsFallback);

public sealed record ChatResponseDto(
    Guid SessionId,
    string Answer,
    IReadOnlyList<CitationDto> Citations,
    string SourcesUsed,
    IReadOnlyList<ChatAttachmentDto> Attachments,
    ChatGenerationDto? Generation = null);

public sealed record ChatCapabilitiesDto(
    bool WebSearchEnabled,
    IReadOnlyList<string> SearchModes,
    bool FileExportEnabled,
    IReadOnlyList<string> ExportFormats);

public sealed record DocumentListItemDto(
    Guid Id,
    string Title,
    string SourceType,
    string? DocType,
    string IngestStatus,
    string? IngestDetail,
    DateTimeOffset CreatedAt,
    Guid? ProjectId = null);

public sealed record IngestUploadResponseDto(
    Guid? RunId,
    int Processed,
    int Failed,
    string Status,
    IReadOnlyList<string> Errors);

public sealed record IngestSharePointRequestDto(
    string SiteUrl,
    string? FolderPath = null,
    string? FilePath = null);

public sealed record IngestJobEnqueueResponseDto(
    Guid JobId,
    string Status);

public sealed record IngestUploadEnqueueDto(
    Guid DocumentId,
    Guid JobId,
    string Status,
    string Message);

public sealed record IngestJobStatusDto(
    Guid Id,
    string JobType,
    string Status,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    string? Instructions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateProjectRequestDto(
    string Name,
    string? Description = null,
    string? Instructions = null);

public sealed record UpdateProjectRequestDto(
    string? Name = null,
    string? Description = null,
    string? Instructions = null);

public sealed record PromptDto(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreatePromptRequestDto(
    string Title,
    string Content,
    Guid? ProjectId = null);

public sealed record UpdatePromptRequestDto(
    string? Title = null,
    string? Content = null);

public sealed record ChatSessionDto(
    Guid Id,
    Guid? ProjectId,
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpdateChatSessionRequestDto(string? Title = null);

public sealed record IngestRunStatusDto(
    Guid Id,
    string SourceType,
    string? SourceLabel,
    string Status,
    int FilesProcessed,
    int FilesFailed,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record ChatMessageDto(
    Guid Id,
    string Role,
    string Content,
    IReadOnlyList<CitationDto>? Citations,
    IReadOnlyList<ChatAttachmentDto>? Attachments,
    DateTimeOffset CreatedAt,
    ChatGenerationDto? Generation = null);
