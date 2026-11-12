namespace Intranet.Api.Help;

/// <summary>
/// Optional rewriter for help answers. Implementations must return null when
/// the model is down or times out so callers can fall back to the curated map.
/// </summary>
public interface IHelpLlm
{
    /// <summary>
    /// True when <c>KnowledgeBase__Fallback__ApiKey</c> is present and hosted
    /// fallback is enabled. Ollama may still serve Help when this is false.
    /// </summary>
    bool IsHostedFallbackConfigured { get; }

    Task<HelpLlmTurn?> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken);
}
