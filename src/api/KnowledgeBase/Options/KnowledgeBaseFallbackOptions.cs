namespace Intranet.Api.KnowledgeBase.Options;

/// <summary>
/// Hosted OpenAI-compatible chat fallback used when the local Ollama GPU VM is down.
/// Bind from <c>KnowledgeBase:Fallback</c> / <c>KnowledgeBase__Fallback__*</c>.
/// Works with api.openai.com and Azure OpenAI (and other OpenAI-compatible gateways).
/// Never commit a real API key.
/// </summary>
public sealed class KnowledgeBaseFallbackOptions
{
    public const string SectionName = "KnowledgeBase:Fallback";

    /// <summary>
    /// When false, never call the hosted model even if a key is present.
    /// Default true; the fallback still stays inactive until an API key is set.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OpenAI: <c>https://api.openai.com/v1</c>.
    /// Azure OpenAI: resource origin or full deployment URL
    /// (<c>https://{resource}.openai.azure.com</c> or
    /// <c>https://{resource}.openai.azure.com/openai/deployments/{deployment}</c>).
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>OpenAI model name, or Azure OpenAI deployment name when the URL has no deployment segment.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Bind from KnowledgeBase__Fallback__ApiKey (env), user secrets, App Settings, or Key Vault.
    /// </summary>
    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Azure OpenAI query parameter. Ignored for api.openai.com.</summary>
    public string ApiVersion { get; set; } = "2024-10-21";

    public string? TrimmedApiKey => string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim();

    public bool IsConfigured =>
        Enabled && TrimmedApiKey is not null && !string.IsNullOrWhiteSpace(BaseUrl);
}
