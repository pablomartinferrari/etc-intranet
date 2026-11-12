using Intranet.Api.KnowledgeBase.Options;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.Services;

public sealed class ChatCompletionRouter
{
    private readonly IChatCompletionClient _ollama;
    private readonly IChatCompletionClient _hosted;
    private readonly IOllamaHealthProbe _health;
    private readonly KnowledgeBaseFallbackOptions _fallback;
    private readonly ILogger<ChatCompletionRouter> _logger;

    public ChatCompletionRouter(
        OllamaClient ollama,
        OpenAiCompatibleChatClient hosted,
        IOllamaHealthProbe health,
        IOptions<KnowledgeBaseOptions> options,
        ILogger<ChatCompletionRouter> logger)
        : this(ollama, hosted, health, options.Value.Fallback, logger)
    {
    }

    public ChatCompletionRouter(
        IChatCompletionClient ollama,
        IChatCompletionClient hosted,
        IOllamaHealthProbe health,
        KnowledgeBaseFallbackOptions fallback,
        ILogger<ChatCompletionRouter> logger)
    {
        _ollama = ollama;
        _hosted = hosted;
        _health = health;
        _fallback = fallback;
        _logger = logger;
    }

    public bool IsFallbackConfigured => _fallback.IsConfigured;

    public async Task<ChatCompletionResult> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var ollamaHealthy = await _health.IsAvailableAsync(cancellationToken);
        if (ollamaHealthy)
        {
            try
            {
                var content = await _ollama.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
                return Served(_ollama, content, isFallback: false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Ollama chat failed after a healthy probe; considering hosted fallback.");
                _health.Invalidate();
            }
        }
        else
        {
            _logger.LogInformation(
                "Ollama is unreachable; considering hosted fallback (configured={Configured}).",
                _fallback.IsConfigured);
        }

        if (_fallback.IsConfigured)
        {
            try
            {
                var content = await _hosted.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
                return Served(_hosted, content, isFallback: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Hosted chat fallback failed.");
                throw new ChatUnavailableException(
                    "Chat is temporarily unavailable. The local knowledge-base model is offline, " +
                    "and the hosted fallback could not complete the request. Try again in a moment.");
            }
        }

        throw new ChatUnavailableException();
    }

    private ChatCompletionResult Served(IChatCompletionClient client, string content, bool isFallback)
    {
        _logger.LogInformation(
            "KB chat generation served by {Provider} model {Model} (fallback={Fallback})",
            client.ProviderName,
            client.ModelName,
            isFallback);
        return new ChatCompletionResult(content, client.ProviderName, client.ModelName, isFallback);
    }
}
