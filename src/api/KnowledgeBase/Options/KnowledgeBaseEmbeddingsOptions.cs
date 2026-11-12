namespace Intranet.Api.KnowledgeBase.Options;

/// <summary>
/// Hosted OpenAI-compatible embeddings used for SharePoint agent-source ingest
/// so the GPU / Ollama VM can stay off. Bind from <c>KnowledgeBase:Embeddings</c>.
/// When ApiKey or BaseUrl is empty, values fall back to <see cref="KnowledgeBaseFallbackOptions"/>.
/// Never commit a real API key.
/// </summary>
public sealed class KnowledgeBaseEmbeddingsOptions
{
    public const string SectionName = "KnowledgeBase:Embeddings";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OpenAI: <c>https://api.openai.com/v1</c>.
    /// Azure OpenAI: resource origin or full deployment URL.
    /// Empty inherits <c>KnowledgeBase:Fallback:BaseUrl</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>OpenAI model name, or Azure deployment name when the URL has no deployment segment.</summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Bind from KnowledgeBase__Embeddings__ApiKey. Empty inherits Fallback:ApiKey.
    /// </summary>
    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Azure OpenAI query parameter. Ignored for api.openai.com.</summary>
    public string ApiVersion { get; set; } = "2024-10-21";

    /// <summary>
    /// Requested output dimensions. Default 768 matches nomic-embed-text / typical etc-kg pgvector columns.
    /// Set null to use the model default (1536 for text-embedding-3-small).
    /// </summary>
    public int? Dimensions { get; set; } = 768;

    public string? TrimmedApiKey => string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim();
}

/// <summary>
/// Self-serve SharePoint folder ingest limits. Bind from <c>KnowledgeBase:AgentSources</c>.
/// </summary>
public sealed class AgentSourceOptions
{
    public const string SectionName = "KnowledgeBase:AgentSources";

    /// <summary>Safety valve so linking a site root cannot recurse forever.</summary>
    public int MaxDepth { get; set; } = 20;

    /// <summary>At or below this size AND file count, ingest runs without extra confirmation.</summary>
    public long SoftMaxBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    public int SoftMaxFiles { get; set; } = 2_000;

    /// <summary>Above soft, at or below this size AND file count: warn and require confirm.</summary>
    public long MediumMaxBytes { get; set; } = 10L * 1024 * 1024 * 1024;

    public int MediumMaxFiles { get; set; } = 10_000;

    /// <summary>Per-file cap; larger files are skipped.</summary>
    public long MaxFileBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>Stop probing after this many items (files + folders) so a TB library cannot hang the request.</summary>
    public int ProbeMaxItems { get; set; } = 50_000;

    public int ProbeTimeoutSeconds { get; set; } = 45;
}
