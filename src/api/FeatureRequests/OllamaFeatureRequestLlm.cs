using Intranet.Api.KnowledgeBase.Services;

namespace Intranet.Api.FeatureRequests;

public sealed class OllamaFeatureRequestLlm(
    OllamaClient ollama,
    ILogger<OllamaFeatureRequestLlm> logger) : IFeatureRequestLlm
{
    public async Task<string?> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ollama.ChatAsync(systemPrompt, userPrompt, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Feature-request LLM is unavailable; using deterministic fallback.");
            return null;
        }
    }
}
