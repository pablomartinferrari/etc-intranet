using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.Services;

public sealed class OllamaClient
{
    private readonly HttpClient _http;
    private readonly KnowledgeBaseOptions _options;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(HttpClient http, IOptions<KnowledgeBaseOptions> options, ILogger<OllamaClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.OllamaBaseUrl.TrimEnd('/');

        (object body, string path)[] attempts =
        [
            (new { model = _options.EmbedModel, input = text }, "/api/embed"),
            (new { model = _options.EmbedModel, prompt = text }, "/api/embeddings"),
        ];

        foreach (var (body, path) in attempts)
        {
            using var response = await _http.PostAsJsonAsync($"{baseUrl}{path}", body, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Ollama embed attempt failed: {Path} {Status} {Body}", path, response.StatusCode, errBody);
                continue;
            }

            var json = await response.Content.ReadFromJsonAsync<OllamaEmbedJsonResponse>(cancellationToken);
            if (json?.Embeddings is { Count: > 0 })
            {
                return json.Embeddings[0];
            }

            if (json?.Embedding is { Length: > 0 })
            {
                return json.Embedding;
            }
        }

        throw new InvalidOperationException(
            $"Ollama embedding failed. Is Ollama running at {_options.OllamaBaseUrl} with model '{_options.EmbedModel}' pulled?");
    }

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var url = $"{_options.OllamaBaseUrl.TrimEnd('/')}/api/chat";
        using var response = await _http.PostAsJsonAsync(
            url,
            new
            {
                model = _options.ChatModel,
                stream = false,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt },
                },
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Ollama chat failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException(
                $"Ollama chat failed ({response.StatusCode}). Is model '{_options.ChatModel}' pulled?");
        }

        var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken);
        var content = payload?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Ollama returned an empty chat response.");
        }

        return content;
    }

    private sealed record OllamaEmbedJsonResponse(
        [property: JsonPropertyName("embedding")] float[]? Embedding,
        [property: JsonPropertyName("embeddings")] List<float[]>? Embeddings);
    private sealed record OllamaChatResponse([property: JsonPropertyName("message")] OllamaChatMessage? Message);
    private sealed record OllamaChatMessage([property: JsonPropertyName("content")] string? Content);
}
