using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.Services;

public sealed class OpenAiCompatibleChatClient : IChatCompletionClient
{
    private readonly HttpClient _http;
    private readonly KnowledgeBaseFallbackOptions _options;
    private readonly ILogger<OpenAiCompatibleChatClient> _logger;

    public OpenAiCompatibleChatClient(
        HttpClient http,
        IOptions<KnowledgeBaseOptions> options,
        ILogger<OpenAiCompatibleChatClient> logger)
    {
        _http = http;
        _options = options.Value.Fallback;
        _logger = logger;
    }

    public string ProviderName => IsAzureOpenAi(_options.BaseUrl) ? "azure-openai" : "openai";

    public string ModelName =>
        string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o-mini" : _options.Model.Trim();

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new ChatUnavailableException();
        }

        using var request = BuildRequest(
            _options.BaseUrl,
            ModelName,
            _options.TrimmedApiKey!,
            _options.ApiVersion,
            systemPrompt,
            userPrompt);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Hosted chat fallback failed: {Provider} {Status}",
                ProviderName,
                response.StatusCode);
            throw new InvalidOperationException(
                $"Hosted chat fallback failed ({response.StatusCode}). Check KnowledgeBase__Fallback__BaseUrl and the API key.");
        }

        var payload = System.Text.Json.JsonSerializer.Deserialize<OpenAiChatResponse>(body);
        var content = payload?.Choices is { Count: > 0 } ? payload.Choices[0].Message?.Content : null;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Hosted chat fallback returned an empty response.");
        }

        return content;
    }

    public static bool IsAzureOpenAi(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Contains("cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase);
    }

    public static HttpRequestMessage BuildRequest(
        string baseUrl,
        string model,
        string apiKey,
        string apiVersion,
        string systemPrompt,
        string userPrompt)
    {
        var azure = IsAzureOpenAi(baseUrl);
        var url = ResolveCompletionsUrl(baseUrl, model, apiVersion, azure);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt },
                },
                temperature = 0.2,
            }),
        };

        if (azure)
        {
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        return request;
    }

    public static string ResolveCompletionsUrl(string baseUrl, string model, string apiVersion, bool azure)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return azure ? AppendApiVersion(trimmed, apiVersion) : trimmed;
        }

        if (azure)
        {
            if (trimmed.Contains("/openai/deployments/", StringComparison.OrdinalIgnoreCase))
            {
                return AppendApiVersion($"{trimmed}/chat/completions", apiVersion);
            }

            return AppendApiVersion(
                $"{trimmed}/openai/deployments/{Uri.EscapeDataString(model)}/chat/completions",
                apiVersion);
        }

        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return $"{trimmed}/chat/completions";
        }

        return $"{trimmed}/v1/chat/completions";
    }

    private static string AppendApiVersion(string url, string apiVersion)
    {
        var version = string.IsNullOrWhiteSpace(apiVersion) ? "2024-10-21" : apiVersion.Trim();
        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}api-version={Uri.EscapeDataString(version)}";
    }

    private sealed record OpenAiChatResponse(
        [property: JsonPropertyName("choices")] List<OpenAiChoice>? Choices);

    private sealed record OpenAiChoice(
        [property: JsonPropertyName("message")] OpenAiMessage? Message);

    private sealed record OpenAiMessage(
        [property: JsonPropertyName("content")] string? Content);
}
