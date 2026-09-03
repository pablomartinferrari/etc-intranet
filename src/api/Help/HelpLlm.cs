using Intranet.Api.KnowledgeBase.Services;

namespace Intranet.Api.Help;

/// <summary>
/// Optional rewriter using the same Ollama → hosted-model router as Knowledge Chat
/// (<see cref="ChatCompletionRouter"/>). Always fails closed so the curated map
/// can answer when models are down. Timeout is long enough for the hosted
/// KnowledgeBase Fallback path when the GPU VM is deallocated.
/// </summary>
public sealed class HelpLlm(
    ChatCompletionRouter chat,
    ILogger<HelpLlm> logger) : IHelpLlm
{
    /// <summary>
    /// Allow the hosted fallback (default 30s HTTP timeout) enough room after the
    /// short Ollama health probe. Eight seconds was cancelling hosted calls in prod.
    /// </summary>
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(25);

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
            logger.LogInformation(ex, "Help LLM is unavailable; answering from the intranet map.");
            return null;
        }
    }
}
