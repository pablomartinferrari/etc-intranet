using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Help;

/// <summary>
/// Optional rewriter using the same Ollama → hosted-model router as Knowledge Chat
/// (<see cref="ChatCompletionRouter"/>). Always fails closed so the curated map
/// can answer when models are down. Timeout is long enough for the hosted
/// KnowledgeBase Fallback path when the GPU VM is deallocated.
/// </summary>
public sealed class HelpLlm(
    ChatCompletionRouter chat,
    IOptions<KnowledgeBaseOptions> options,
    ILogger<HelpLlm> logger) : IHelpLlm
{
    /// <summary>
    /// Slack added to the hosted HTTP timeout so Help does not cancel a live
    /// fallback call (Ollama probe is ~2s; hosted client default is 30s).
    /// Eight seconds and later 25 seconds were both shorter than that HTTP timeout
    /// and cancelled Help AI in prod even when <c>KnowledgeBase__Fallback__ApiKey</c>
    /// was set.
    /// </summary>
    public const int TimeoutSlackSeconds = 10;

    public bool IsHostedFallbackConfigured => chat.IsFallbackConfigured;

    public TimeSpan CallTimeout { get; } = ResolveTimeout(options.Value.Fallback);

    /// <summary>Hosted fallback HTTP timeout plus slack for the Ollama probe.</summary>
    public static TimeSpan ResolveTimeout(KnowledgeBaseFallbackOptions fallback)
    {
        var hosted = fallback.TimeoutSeconds > 0 ? fallback.TimeoutSeconds : 30;
        return TimeSpan.FromSeconds(hosted + TimeoutSlackSeconds);
    }

    public async Task<HelpLlmTurn?> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CallTimeout);
            var result = await chat.CompleteAsync(systemPrompt, userPrompt, cts.Token);
            if (string.IsNullOrWhiteSpace(result.Content))
            {
                return null;
            }

            return new HelpLlmTurn(result.Content, result.Provider, result.Model, result.IsFallback);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (chat.IsFallbackConfigured)
            {
                logger.LogWarning(ex, "Help LLM failed despite hosted fallback; answering from the intranet map.");
            }
            else
            {
                logger.LogInformation(ex, "Help LLM is unavailable (no hosted fallback); answering from the intranet map.");
            }

            return null;
        }
    }
}
