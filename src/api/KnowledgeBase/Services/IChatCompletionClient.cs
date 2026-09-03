namespace Intranet.Api.KnowledgeBase.Services;

public interface IChatCompletionClient
{
    string ProviderName { get; }
    string ModelName { get; }

    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}

public sealed record ChatCompletionResult(
    string Content,
    string Provider,
    string Model,
    bool IsFallback);

public sealed class ChatUnavailableException : InvalidOperationException
{
    public const string UserMessage =
        "Chat is temporarily unavailable. The local knowledge-base model is offline, " +
        "and no hosted fallback is configured. " +
        "Add KnowledgeBase__Fallback__ApiKey (dotnet user-secrets locally, or an App Setting / Key Vault secret in Azure), " +
        "or start the GPU VM that runs Ollama.";

    public ChatUnavailableException()
        : base(UserMessage)
    {
    }

    public ChatUnavailableException(string message)
        : base(message)
    {
    }
}
