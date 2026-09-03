using Intranet.Api.KnowledgeBase.Services;

namespace Intranet.Api.Help;

/// <summary>
/// Optional rewriter using the same Ollama → hosted-model router as Knowledge Chat.
/// Always fails closed so the curated map can answer when models are down.
/// </summary>
public sealed class HelpLlm(
    ChatCompletionRouter chat,
    ILogger<HelpLlm> logger) : IHelpLlm
{
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(8);

    public async Task<string?> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CallTimeout);
            var result = await chat.CompleteAsync(systemPrompt, userPrompt, cts.Token);
            return string.IsNullOrWhiteSpace(result.Content) ? null : result.Content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(ex, "Help LLM is unavailable; answering from the intranet map.");
            return null;
        }
    }
}
