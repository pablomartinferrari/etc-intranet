using Intranet.Api.KnowledgeBase.Services;

namespace Intranet.Api.Help;

/// <summary>
/// Optional Ollama rewriter. Fails fast and caches a short "down" window so
/// the help panel never waits on a deallocated GPU VM.
/// </summary>
public sealed class OllamaHelpLlm(
    OllamaClient ollama,
    ILogger<OllamaHelpLlm> logger) : IHelpLlm
{
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan DownCacheTtl = TimeSpan.FromSeconds(45);

    private readonly object _gate = new();
    private DateTimeOffset _downUntil = DateTimeOffset.MinValue;

    public async Task<string?> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (DateTimeOffset.UtcNow < _downUntil)
            {
                return null;
            }
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CallTimeout);
            var content = await ollama.ChatAsync(systemPrompt, userPrompt, cts.Token);
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(ex, "Help Ollama call failed; answering from the intranet map.");
            lock (_gate)
            {
                _downUntil = DateTimeOffset.UtcNow.Add(DownCacheTtl);
            }

            return null;
        }
    }
}
