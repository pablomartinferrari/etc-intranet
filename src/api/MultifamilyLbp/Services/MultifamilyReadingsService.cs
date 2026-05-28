using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Intranet.Api.MultifamilyLbp.Models;
using Intranet.Api.MultifamilyLbp.Options;

namespace Intranet.Api.MultifamilyLbp.Services;

public sealed class MultifamilyReadingsService
{
    /// <summary>SharePoint requires this when filtering/ordering by non-indexed columns (e.g. JobNumber). Prefer indexing those columns in the library for production.</summary>
    private const string PreferHonorNonIndexedQueries = "HonorNonIndexedQueriesWarningMayFailRandomly";

    private static readonly Regex SchemeWhitespaceRegex = new(@"^(https?):\s*//\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private readonly IParserClient _parser;
    private readonly IOptions<AzureAdOptions> _azureAd;
    private readonly IOptions<SharePointOptions> _sharePoint;
    private readonly ILogger<MultifamilyReadingsService> _logger;

    public MultifamilyReadingsService(
        IParserClient parser,
        IOptions<AzureAdOptions> azureAd,
        IOptions<SharePointOptions> sharePoint,
        ILogger<MultifamilyReadingsService> logger)
    {
        _parser = parser;
        _azureAd = azureAd;
        _sharePoint = sharePoint;
        _logger = logger;
    }

    public async Task<IReadOnlyList<XrfReadingDto>> GetReadingsAsync(string jobNumber, string areaTypeSharePoint, CancellationToken cancellationToken = default)
    {
        var opts = _azureAd.Value;
        var sp = _sharePoint.Value;
        if (string.IsNullOrWhiteSpace(opts.TenantId) || string.IsNullOrWhiteSpace(opts.ClientId) || string.IsNullOrWhiteSpace(opts.ClientSecret))
            throw new InvalidOperationException("AzureAd:TenantId, ClientId, and ClientSecret must be configured for Graph access.");

        if (string.IsNullOrWhiteSpace(sp.SiteUrl))
            throw new InvalidOperationException("SharePoint:SiteUrl must be configured.");

        if (!TryBuildGraphSiteKey(sp.SiteUrl, out var siteKey))
            throw new InvalidOperationException(
                "SharePoint:SiteUrl is not a valid absolute http(s) URL. Remove spaces (e.g. use https:// not https: //) and prefer the site root, e.g. https://tenant.sharepoint.com/sites/YourSite");

        var credential = new ClientSecretCredential(opts.TenantId, opts.ClientId, opts.ClientSecret);
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var graphClient = new GraphServiceClient(credential, scopes);

        Site? site;
        try
        {
            site = await graphClient.Sites[siteKey].GetAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve SharePoint site {SiteKey}", siteKey);
            throw new InvalidOperationException($"Could not resolve SharePoint site. Check SharePoint:SiteUrl and app permissions. Site key: {siteKey}", ex);
        }

        if (string.IsNullOrEmpty(site?.Id))
            throw new InvalidOperationException("SharePoint site has no id.");

        var libName = EscapeODataString(sp.SourceLibraryName);
        var lists = await graphClient.Sites[site.Id].Lists.GetAsync(requestConfiguration =>
        {
            requestConfiguration.QueryParameters.Filter = $"displayName eq '{libName}'";
        }, cancellationToken);

        var list = lists?.Value?.FirstOrDefault();
        if (list?.Id == null)
            throw new InvalidOperationException($"List '{sp.SourceLibraryName}' not found on site.");

        var jobEsc = EscapeODataString(jobNumber.Trim());
        // Filter by job only; SharePoint choice OData often mismatches (spacing/casing/shape). AreaType is applied in-process.
        var filter = $"fields/JobNumber eq '{jobEsc}'";
        var requestedArea = CanonicalizeRequestedAreaType(areaTypeSharePoint);

        var items = await graphClient.Sites[site.Id].Lists[list.Id].Items.GetAsync(requestConfiguration =>
        {
            requestConfiguration.QueryParameters.Filter = filter;
            requestConfiguration.QueryParameters.Expand = new[] { "driveItem", "fields" };
            requestConfiguration.QueryParameters.Orderby = new[] { "createdDateTime asc" };
            AddHonorNonIndexedQueriesPreferHeader(requestConfiguration.Headers);
        }, cancellationToken);

        var listItems = items?.Value?.ToList() ?? new List<ListItem>();
        // Simple pagination
        var next = items?.OdataNextLink;
        while (!string.IsNullOrEmpty(next))
        {
            var page = await graphClient.Sites[site.Id].Lists[list.Id].Items.WithUrl(next).GetAsync(requestConfiguration =>
            {
                AddHonorNonIndexedQueriesPreferHeader(requestConfiguration.Headers);
            }, cancellationToken);
            if (page?.Value != null) listItems.AddRange(page.Value);
            next = page?.OdataNextLink;
        }

        var jobOnlyItems = listItems;
        var filteredItems = FilterListItemsByAreaType(jobOnlyItems, requestedArea, jobNumber, _logger);
        if (filteredItems.Count == 0 && jobOnlyItems.Count > 0)
        {
            _logger.LogWarning(
                "Job {Job}: {Count} file(s) matched job number but none matched AreaType {Area} after normalizing metadata. Retrying with strict OData filter.",
                jobNumber,
                jobOnlyItems.Count,
                requestedArea);
            var areaEsc = EscapeODataString(areaTypeSharePoint.Trim());
            var strictFilter = $"fields/JobNumber eq '{jobEsc}' and fields/AreaType eq '{areaEsc}'";
            var strict = await graphClient.Sites[site.Id].Lists[list.Id].Items.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = strictFilter;
                requestConfiguration.QueryParameters.Expand = new[] { "driveItem", "fields" };
                requestConfiguration.QueryParameters.Orderby = new[] { "createdDateTime asc" };
                AddHonorNonIndexedQueriesPreferHeader(requestConfiguration.Headers);
            }, cancellationToken);
            var strictItems = strict?.Value?.ToList() ?? [];
            next = strict?.OdataNextLink;
            while (!string.IsNullOrEmpty(next))
            {
                var page = await graphClient.Sites[site.Id].Lists[list.Id].Items.WithUrl(next).GetAsync(requestConfiguration =>
                {
                    AddHonorNonIndexedQueriesPreferHeader(requestConfiguration.Headers);
                }, cancellationToken);
                if (page?.Value != null) strictItems.AddRange(page.Value);
                next = page?.OdataNextLink;
            }

            if (strictItems.Count > 0)
                filteredItems = strictItems;
            else
                _logger.LogWarning(
                    "Job {Job}: strict OData AreaType filter returned 0 items; keeping prior filtered set (empty). Job-only had {Count} item(s).",
                    jobNumber,
                    jobOnlyItems.Count);
        }

        var files = new List<(string FileName, byte[] Content)>();
        foreach (var item in filteredItems)
        {
            var driveItem = item.DriveItem;
            if (driveItem?.Id == null || string.IsNullOrEmpty(driveItem.ParentReference?.DriveId))
                continue;

            await using var stream = await graphClient.Drives[driveItem.ParentReference.DriveId].Items[driveItem.Id]
                .Content.GetAsync(cancellationToken: cancellationToken);
            if (stream == null) continue;
            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            var name = driveItem.Name ?? "file.xlsx";
            files.Add((name, ms.ToArray()));
        }

        return await MergeReadingsAsync(files, requestedArea, cancellationToken);
    }

    private static List<ListItem> FilterListItemsByAreaType(
        IReadOnlyList<ListItem> listItems,
        string requestedArea,
        string jobNumber,
        ILogger logger)
    {
        var result = new List<ListItem>();
        var anyKnownArea = listItems.Any(i =>
            CanonicalizeStoredAreaType(TryGetListItemFieldValue(i, "AreaType")) != null);

        foreach (var item in listItems)
        {
            var storedArea = CanonicalizeStoredAreaType(TryGetListItemFieldValue(item, "AreaType"));
            if (storedArea == requestedArea)
                result.Add(item);
            else if (storedArea == null && !anyKnownArea)
            {
                // Graph often omits choice values in expanded fields; if nothing parsed, do not drop every file.
                logger.LogDebug(
                    "Including list item {ItemId} for job {Job}: AreaType not readable (no known values on batch).",
                    item.Id,
                    jobNumber);
                result.Add(item);
            }
            else if (storedArea != null)
                logger.LogDebug(
                    "Skipping list item {ItemId} for job {Job}: AreaType is {Stored}, requested {Requested}",
                    item.Id,
                    jobNumber,
                    storedArea,
                    requestedArea);
        }

        return result;
    }

    /// <summary>Graph / SharePoint may return choice as string, object with Value/LookupValue, or JsonElement.</summary>
    private static object? TryGetListItemFieldValue(ListItem item, string fieldName)
    {
        var fields = item.Fields;
        if (fields == null) return null;
        if (fields.AdditionalData != null)
        {
            foreach (var kv in fields.AdditionalData)
            {
                if (string.Equals(kv.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(fields);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (!string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase)) continue;
                var el = p.Value;
                if (el.ValueKind == JsonValueKind.String) return el.GetString();
                if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("Value", out var inner))
                    return inner.ValueKind == JsonValueKind.String ? inner.GetString() : inner.GetRawText();
                return el.GetRawText();
            }
        }
        catch
        {
            // ignore serialization fallback
        }

        return null;
    }

    private static string? CanonicalizeStoredAreaType(object? raw) =>
        raw == null ? null : CanonicalizeAreaTypeString(FieldValueToDisplayString(raw));

    private static string CanonicalizeRequestedAreaType(string areaTypeSharePoint)
    {
        var c = CanonicalizeAreaTypeString(areaTypeSharePoint);
        return c ?? areaTypeSharePoint.Trim();
    }

    /// <summary>Maps stored / UI variants to <c>Units</c> or <c>Common Areas</c>.</summary>
    private static string? CanonicalizeAreaTypeString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        var compact = t.Replace(" ", "", StringComparison.Ordinal).Replace("\u00a0", "", StringComparison.Ordinal);
        if (t.Equals("Units", StringComparison.OrdinalIgnoreCase) || compact.Equals("Units", StringComparison.OrdinalIgnoreCase))
            return "Units";
        if (t.Equals("Common Areas", StringComparison.OrdinalIgnoreCase) ||
            compact.Equals("CommonAreas", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Common Area", StringComparison.OrdinalIgnoreCase) ||
            compact.Equals("CommonArea", StringComparison.OrdinalIgnoreCase))
            return "Common Areas";
        return t;
    }

    private static string? FieldValueToDisplayString(object? v)
    {
        if (v == null) return null;
        if (v is string s) return s;
        if (v is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String) return je.GetString();
            if (je.TryGetProperty("Value", out var val))
                return val.ValueKind == JsonValueKind.String ? val.GetString() : val.ToString();
            if (je.TryGetProperty("LookupValue", out var lv))
                return lv.ValueKind == JsonValueKind.String ? lv.GetString() : lv.ToString();
            return je.ToString();
        }

        return v.ToString();
    }

    private async Task<IReadOnlyList<XrfReadingDto>> MergeReadingsAsync(
        IReadOnlyList<(string FileName, byte[] Content)> filesOrdered,
        string areaTypeLabel,
        CancellationToken cancellationToken)
    {
        var merged = new Dictionary<string, XrfReadingDto>(StringComparer.Ordinal);
        for (var i = 0; i < filesOrdered.Count; i++)
        {
            var (fileName, content) = filesOrdered[i];
            IReadOnlyList<XrfReadingDto> readings;
            try
            {
                readings = await _parser.ParseFileAsync(content, fileName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parse failed for {File}", fileName);
                throw;
            }

            foreach (var r in readings)
            {
                var key = $"{i}_{r.ReadingId}";
                var copy = CloneWithArea(r, areaTypeLabel, i);
                merged[key] = copy;
            }
        }

        return merged.Values.ToList();
    }

    private static XrfReadingDto CloneWithArea(XrfReadingDto r, string areaTypeLabel, int fileIndex)
    {
        var suffix = $"_f{fileIndex}";
        return new XrfReadingDto
        {
            ReadingId = r.ReadingId + suffix,
            Component = r.Component,
            Color = r.Color,
            LeadContent = r.LeadContent,
            NormalizedComponent = r.NormalizedComponent,
            NormalizedSubstrate = r.NormalizedSubstrate,
            IsPositive = r.IsPositive,
            Location = r.Location,
            UnitNumber = r.UnitNumber,
            Multifamily = r.Multifamily,
            SiteAddress = r.SiteAddress,
            RoomType = r.RoomType,
            RoomNumber = r.RoomNumber,
            Substrate = r.Substrate,
            Side = r.Side,
            Condition = r.Condition,
            Timestamp = r.Timestamp,
            AreaType = areaTypeLabel,
            RawRow = r.RawRow,
        };
    }

    private static void AddHonorNonIndexedQueriesPreferHeader(RequestHeaders headers) =>
        headers.TryAdd("Prefer", PreferHonorNonIndexedQueries);

    private static string EscapeODataString(string s) => s.Replace("'", "''", StringComparison.Ordinal);

    /// <summary>
    /// Normalizes pasted SharePoint URLs and builds the Graph site key <c>{host}:{path}</c>.
    /// Strips trailing paths like <c>/SitePages/Home.aspx</c> so the key points at the site collection.
    /// </summary>
    private static bool TryBuildGraphSiteKey(string raw, out string siteKey)
    {
        siteKey = "";
        var normalized = raw.Trim();
        // Paste errors: "https: //host" or extra spaces after scheme
        normalized = SchemeWhitespaceRegex.Replace(normalized, m => $"{m.Groups[1].Value}://");

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = uri.Host;
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimEnd('/');
        path = TrimToSharePointSiteCollectionPath(path);
        if (string.IsNullOrEmpty(path))
            path = "/";

        siteKey = $"{host}:{path}";
        return true;
    }

    private static string TrimToSharePointSiteCollectionPath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath) || absolutePath == "/")
            return absolutePath;

        var segments = absolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            var root = segments[0];
            if (root.Equals("sites", StringComparison.OrdinalIgnoreCase) ||
                root.Equals("teams", StringComparison.OrdinalIgnoreCase))
                return "/" + root + "/" + segments[1];
        }

        return absolutePath;
    }
}
