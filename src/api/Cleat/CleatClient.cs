using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Cleat;

public sealed partial class CleatClient(
    HttpClient http,
    IOptions<CleatOptions> options,
    ILogger<CleatClient> logger)
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly CleatOptions _options = options.Value;

    public bool HasApiKey => _options.HasApiKey;

    public async Task<RecommendationListDto> GetRecommendationsAsync(
        double minScore,
        int limit,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var query = new Dictionary<string, string?>
        {
            ["min_score"] = minScore.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        using var document = await SendAsync(QueryHelpers.AddQueryString("v1/recommendations", query), cancellationToken);
        return CleatJsonMapper.MapRecommendations(document.RootElement.Clone(), _options.AppBaseUrl);
    }

    public async Task<OpportunityDto?> GetOpportunityAsync(string opportunityId, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (!OpportunityIdPattern().IsMatch(opportunityId))
        {
            return null;
        }

        using var document = await SendAsync(
            "v1/opportunities/" + Uri.EscapeDataString(opportunityId),
            cancellationToken);
        return CleatJsonMapper.TryMapOpportunity(document.RootElement.Clone(), _options.AppBaseUrl);
    }

    public static bool IsValidOpportunityId(string? opportunityId) =>
        !string.IsNullOrWhiteSpace(opportunityId) && OpportunityIdPattern().IsMatch(opportunityId);

    private void EnsureConfigured()
    {
        if (!_options.HasApiKey)
        {
            throw new CleatNotConfiguredException();
        }
    }

    private async Task<JsonDocument> SendAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("CLEATUS request timed out for {Path}", SafePath(relativeUrl));
            throw new CleatUpstreamException("CLEATUS did not respond in time. Try again in a moment.", 504, "cleat_timeout");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "CLEATUS request failed for {Path}", SafePath(relativeUrl));
            throw new CleatUpstreamException("Could not reach CLEATUS. Check network access to api.cleat.ai.", 502);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new CleatUpstreamException("Opportunity not found in CLEATUS.", 404, "cleat_not_found");
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                logger.LogWarning("CLEATUS rejected the API key with status {Status}", (int)response.StatusCode);
                throw new CleatUpstreamException(
                    "CLEATUS rejected the configured API key. Confirm Cleat__ApiKey is valid.",
                    502,
                    "cleat_unauthorized");
            }

            if ((int)response.StatusCode == 429)
            {
                logger.LogWarning("CLEATUS rate-limited the request");
                throw new CleatUpstreamException(
                    "CLEATUS rate limit reached (about 100 reads/min). Wait and refresh.",
                    503,
                    "cleat_rate_limited");
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "CLEATUS returned status {Status} for {Path}",
                    (int)response.StatusCode,
                    SafePath(relativeUrl));
                throw new CleatUpstreamException(
                    $"CLEATUS returned HTTP {(int)response.StatusCode}. Try again later.",
                    502);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            try
            {
                return await JsonDocument.ParseAsync(stream, ParseOptions, cancellationToken);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "CLEATUS returned non-JSON for {Path}", SafePath(relativeUrl));
                throw new CleatUpstreamException("CLEATUS returned a response that was not valid JSON.", 502);
            }
        }
    }

    private static string SafePath(string relativeUrl)
    {
        var q = relativeUrl.IndexOf('?', StringComparison.Ordinal);
        return q < 0 ? relativeUrl : relativeUrl[..q];
    }

    // OpenAPI: contract_*, forecast_*, and pursuit IDs (pur_*).
    [GeneratedRegex("^[A-Za-z0-9_-]{1,200}$", RegexOptions.CultureInvariant)]
    private static partial Regex OpportunityIdPattern();
}
