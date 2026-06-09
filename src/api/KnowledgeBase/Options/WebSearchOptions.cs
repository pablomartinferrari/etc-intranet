namespace Intranet.Api.KnowledgeBase.Options;

public sealed class WebSearchOptions
{
    public const string SectionName = "WebSearch";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Tavily";
    public string ApiKey { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 5;

    /// <summary>When SearchMode is auto, fall back to web if best doc score is below this.</summary>
    public double DocRelevanceThreshold { get; set; } = 0.35;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
