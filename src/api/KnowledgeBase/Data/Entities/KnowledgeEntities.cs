namespace Intranet.Api.KnowledgeBase.Data.Entities;

public sealed class KbDocument
{
    public Guid Id { get; set; }
    public Guid? IngestRunId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? SourceUri { get; set; }
    public string? ExternalId { get; set; }
    public string? MimeType { get; set; }
    public string? DocType { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string StorageUri { get; set; } = string.Empty;
    public string IngestStatus { get; set; } = "pending";
    public string? IngestDetail { get; set; }
    public string? UploadedByOid { get; set; }
    public Guid? IngestJobId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<KbChunk> Chunks { get; set; } = [];
}

public sealed class KbProject
{
    public Guid Id { get; set; }
    public string UserOid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class KbProjectDocument
{
    public Guid ProjectId { get; set; }
    public Guid DocumentId { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    public KbProject Project { get; set; } = null!;
    public KbDocument Document { get; set; } = null!;
}

public sealed class KbPrompt
{
    public Guid Id { get; set; }
    public string UserOid { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public KbProject? Project { get; set; }
}

public sealed class KbChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? TokenCount { get; set; }
    public int? PageNumber { get; set; }
    public string? Section { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public KbDocument Document { get; set; } = null!;
    public KbEmbedding? Embedding { get; set; }
}

public sealed class KbEmbedding
{
    public Guid Id { get; set; }
    public Guid ChunkId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public KbChunk Chunk { get; set; } = null!;
}

public sealed class KbIngestRun
{
    public Guid Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? SourceLabel { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int FilesProcessed { get; set; }
    public int FilesFailed { get; set; }
    public string Status { get; set; } = "running";
    public string? ErrorMessage { get; set; }
}

public sealed class KbChatSession
{
    public Guid Id { get; set; }
    public string? UserOid { get; set; }
    public Guid? ProjectId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<KbChatMessage> Messages { get; set; } = [];
}

public sealed class KbChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? CitationsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public KbChatSession Session { get; set; } = null!;
}

public sealed class KbGeneratedFile
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? MessageId { get; set; }
    public string? UserOid { get; set; }
    public Guid? ProjectId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public KbChatSession Session { get; set; } = null!;
}
