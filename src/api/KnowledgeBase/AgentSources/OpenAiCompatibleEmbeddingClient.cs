using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public interface IHostedEmbeddingClient
{
    bool IsConfigured { get; }
    string ModelName { get; }
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}

public sealed class OpenAiCompatibleEmbeddingClient : IHostedEmbeddingClient
{
    private readonly HttpClient _http;
    private readonly KnowledgeBaseEmbeddingsOptions _embeddings;
    private readonly KnowledgeBaseFallbackOptions _fallback;
    private readonly ILogger<OpenAiCompatibleEmbeddingClient> _logger;

    public OpenAiCompatibleEmbeddingClient(
        HttpClient http,
        IOptions<KnowledgeBaseOptions> options,
        ILogger<OpenAiCompatibleEmbeddingClient> logger)
    {
        _http = http;
        _embeddings = options.Value.Embeddings;
        _fallback = options.Value.Fallback;
        _logger = logger;
    }

    public bool IsConfigured =>
        _embeddings.Enabled
        && !string.IsNullOrWhiteSpace(ResolvedApiKey)
        && !string.IsNullOrWhiteSpace(ResolvedBaseUrl);

    public string ModelName =>
        string.IsNullOrWhiteSpace(_embeddings.Model) ? "text-embedding-3-small" : _embeddings.Model.Trim();

    private string ResolvedBaseUrl =>
        string.IsNullOrWhiteSpace(_embeddings.BaseUrl) ? _fallback.BaseUrl : _embeddings.BaseUrl;

    private string? ResolvedApiKey => _embeddings.TrimmedApiKey ?? _fallback.TrimmedApiKey;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new AgentSourceException(
                "Hosted embeddings are not configured. Set KnowledgeBase__Embeddings__ApiKey (or KnowledgeBase__Fallback__ApiKey).",
                503);
        }

        if (inputs.Count == 0)
        {
            return [];
        }

        var azure = OpenAiCompatibleChatClient.IsAzureOpenAi(ResolvedBaseUrl);
        var url = ResolveEmbeddingsUrl(ResolvedBaseUrl, ModelName, _embeddings.ApiVersion, azure);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = ModelName,
            ["input"] = inputs,
        };
        if (_embeddings.Dimensions is > 0)
        {
            payload["dimensions"] = _embeddings.Dimensions.Value;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        if (azure)
        {
            request.Headers.TryAddWithoutValidation("api-key", ResolvedApiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ResolvedApiKey);
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Hosted embeddings failed: {Status} {Body}", response.StatusCode, Truncate(body));
            throw new AgentSourceException(
                $"Hosted embeddings failed ({(int)response.StatusCode}). Check KnowledgeBase__Embeddings__BaseUrl, model, and API key.",
                502);
        }

        var parsed = JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(body);
        var ordered = parsed?.Data?
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding ?? [])
            .ToList() ?? [];
        if (ordered.Count != inputs.Count)
        {
            throw new AgentSourceException("Hosted embeddings returned a different number of vectors than inputs.", 502);
        }

        return ordered;
    }

    public static string ResolveEmbeddingsUrl(string baseUrl, string model, string apiVersion, bool azure)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith("/embeddings", StringComparison.OrdinalIgnoreCase))
        {
            return azure ? AppendApiVersion(trimmed, apiVersion) : trimmed;
        }

        if (azure)
        {
            if (trimmed.Contains("/openai/deployments/", StringComparison.OrdinalIgnoreCase))
            {
                return AppendApiVersion($"{trimmed}/embeddings", apiVersion);
            }

            return AppendApiVersion(
                $"{trimmed}/openai/deployments/{Uri.EscapeDataString(model)}/embeddings",
                apiVersion);
        }

        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return $"{trimmed}/embeddings";
        }

        return $"{trimmed}/v1/embeddings";
    }

    private static string AppendApiVersion(string url, string apiVersion)
    {
        var version = string.IsNullOrWhiteSpace(apiVersion) ? "2024-10-21" : apiVersion.Trim();
        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}api-version={Uri.EscapeDataString(version)}";
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300];

    private sealed record OpenAiEmbeddingResponse(
        [property: JsonPropertyName("data")] List<OpenAiEmbeddingItem>? Data);

    private sealed record OpenAiEmbeddingItem(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
