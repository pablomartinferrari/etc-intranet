using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.Services;

public sealed record WebSearchResult(string Title, string Url, string Snippet);

public sealed class WebSearchService
{
    private readonly HttpClient _http;
    private readonly WebSearchOptions _options;
    private readonly ILogger<WebSearchService> _logger;

    public WebSearchService(
        HttpClient http,
        IOptions<WebSearchOptions> options,
        ILogger<WebSearchService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsAvailable => _options.IsConfigured;

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                "Web search is not configured. Set WebSearch:Enabled=true and WebSearch:ApiKey in appsettings or user secrets.");
        }

        return _options.Provider.Equals("Tavily", StringComparison.OrdinalIgnoreCase)
            ? await SearchTavilyAsync(query, cancellationToken)
            : throw new NotSupportedException($"Web search provider '{_options.Provider}' is not supported.");
    }

    private async Task<IReadOnlyList<WebSearchResult>> SearchTavilyAsync(
        string query,
        CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            "https://api.tavily.com/search",
            new
            {
                api_key = _options.ApiKey,
                query,
                max_results = _options.MaxResults,
                search_depth = "basic",
                include_answer = false,
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Tavily search failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Web search failed ({response.StatusCode}).");
        }

        var payload = await response.Content.ReadFromJsonAsync<TavilyResponse>(cancellationToken);
        if (payload?.Results is null || payload.Results.Count == 0)
        {
            return [];
        }

        return payload.Results
            .Where(r => !string.IsNullOrWhiteSpace(r.Url))
            .Select(r => new WebSearchResult(
                r.Title ?? r.Url!,
                r.Url!,
                Truncate(r.Content ?? string.Empty, 500)))
            .ToList();
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "...";

    private sealed record TavilyResponse(
        [property: JsonPropertyName("results")] List<TavilyResult>? Results);

    private sealed record TavilyResult(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("content")] string? Content);
}
