namespace Intranet.Api.Data.Entities;

public static class AgentSourceStatuses
{
    public const string Connected = "connected";
    public const string Disconnected = "disconnected";
    public const string AwaitingApproval = "awaiting_approval";
}

public static class AgentSourceJobStatuses
{
    public const string Queued = "queued";
    public const string Probing = "probing";
    public const string Running = "running";
    public const string Done = "done";
    public const string Failed = "failed";
    public const string AwaitingApproval = "awaiting_approval";
}

public sealed class AgentSource
{
    public Guid Id { get; set; }
    public string CreatedByOid { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string SiteUrl { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string FolderIdentity { get; set; } = string.Empty;
    public string Status { get; set; } = AgentSourceStatuses.Connected;
    public Guid? LatestJobId { get; set; }
    public int? ApprovalRequestId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DisconnectedAt { get; set; }

    public ICollection<AgentSourceJob> Jobs { get; set; } = [];
    public ICollection<AgentSourceDocument> Documents { get; set; } = [];
}

public sealed class AgentSourceJob
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public string Status { get; set; } = AgentSourceJobStatuses.Queued;
    public string LimitTier { get; set; } = "soft";
    public bool ConfirmedMedium { get; set; }
    public int ProbeFileCount { get; set; }
    public long ProbeTotalBytes { get; set; }
    public int ProbeMaxDepth { get; set; }
    public int ProbeAllowedFiles { get; set; }
    public long ProbeAllowedBytes { get; set; }
    public int ProbeSkippedFiles { get; set; }
    public string? ProbeSampleExtensionsJson { get; set; }
    public bool ProbeTruncated { get; set; }
    public string? ErrorMessage { get; set; }
    public int FilesProcessed { get; set; }
    public int FilesFailed { get; set; }
    public int FilesSkipped { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public AgentSource Source { get; set; } = null!;
}

public sealed class AgentSourceDocument
{
    public Guid SourceId { get; set; }
    public Guid DocumentId { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    public AgentSource Source { get; set; } = null!;
}
