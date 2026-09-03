namespace Intranet.Api.KnowledgeBase.Models;

public sealed record AgentSourceCapabilitiesDto(
    bool GraphConfigured,
    bool EmbeddingsConfigured,
    int SoftMaxFiles,
    long SoftMaxBytes,
    int MediumMaxFiles,
    long MediumMaxBytes,
    long MaxFileBytes,
    int MaxDepth);

public sealed record AgentSourceProbeRequestDto(
    string? SiteUrl,
    string? FolderPath = null);

public sealed record AgentSourceConnectRequestDto(
    string? SiteUrl,
    string? FolderPath = null,
    string? Label = null,
    bool ConfirmMedium = false);

public sealed record AgentSourceProbeDto(
    string SiteUrl,
    string FolderPath,
    string DisplayPath,
    int FileCount,
    long TotalBytes,
    string TotalBytesLabel,
    int AllowedFiles,
    long AllowedBytes,
    string AllowedBytesLabel,
    int SkippedFiles,
    int MaxDepth,
    IReadOnlyList<string> SampleExtensions,
    bool Truncated,
    string LimitTier,
    bool CanAutoRun,
    bool RequiresConfirm,
    bool RequiresApproval,
    string Summary);

public sealed record AgentSourceJobDto(
    Guid Id,
    Guid SourceId,
    string Status,
    string LimitTier,
    int ProbeAllowedFiles,
    long ProbeAllowedBytes,
    int ProbeSkippedFiles,
    IReadOnlyList<string> SampleExtensions,
    bool ProbeTruncated,
    string? ErrorMessage,
    int FilesProcessed,
    int FilesFailed,
    int FilesSkipped,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record AgentSourceDto(
    Guid Id,
    string? Label,
    string SiteUrl,
    string FolderPath,
    string DisplayPath,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DisconnectedAt,
    int? ApprovalRequestId,
    AgentSourceJobDto? LatestJob);
