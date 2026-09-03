using System.Text.RegularExpressions;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public sealed record SharePointFolderRef(
    string SiteUrl,
    string FolderPath,
    string SiteKey,
    string DisplayPath);

public static class SharePointFolderUrlParser
{
    private static readonly Regex SchemeWhitespaceRegex = new(
        @"^(https?):\s*//\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    public static bool TryParse(string? siteUrl, string? folderPath, out SharePointFolderRef? folder, out string? error)
    {
        folder = null;
        error = null;

        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            error = "Paste a SharePoint site URL.";
            return false;
        }

        var normalized = NormalizePastedUrl(siteUrl);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            error = "The SharePoint URL must be an http(s) address, for example https://contoso.sharepoint.com/sites/HR.";
            return false;
        }

        if (uri.Host.Contains("sharepoint.com", StringComparison.OrdinalIgnoreCase) is false
            && uri.Host.Contains("sharepoint", StringComparison.OrdinalIgnoreCase) is false
            && !uri.Host.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase))
        {
            // Still allow on-prem / custom hosts; only reject obviously non-web values.
        }

        if (LooksLikeSharingLink(uri))
        {
            error = "Sharing links (:f:/s/…) are not supported yet. Paste the site URL (https://tenant.sharepoint.com/sites/Name) and the folder path.";
            return false;
        }

        if (!TryBuildGraphSiteKey(uri, out var siteKey, out var remainderFromUrl))
        {
            error = "Could not read a SharePoint site from that URL. Use the site root, for example https://tenant.sharepoint.com/sites/YourSite.";
            return false;
        }

        var fromQuery = FolderFromQuery(uri);
        var explicitPath = NormalizeFolderPath(folderPath);
        var fromUrl = NormalizeFolderPath(remainderFromUrl);
        var resolvedFolder = FirstNonEmpty(explicitPath, fromQuery, fromUrl);

        var siteUrlCanonical = $"{uri.Scheme}://{uri.Host}{SiteCollectionPath(uri)}";
        folder = new SharePointFolderRef(
            siteUrlCanonical.TrimEnd('/'),
            resolvedFolder,
            siteKey,
            string.IsNullOrEmpty(resolvedFolder) ? siteUrlCanonical : $"{siteUrlCanonical}/{resolvedFolder}");
        return true;
    }

    public static string FolderIdentity(SharePointFolderRef folder) =>
        $"{folder.SiteKey}|{folder.FolderPath.ToLowerInvariant()}";

    internal static string NormalizePastedUrl(string raw)
    {
        var normalized = raw.Trim();
        normalized = SchemeWhitespaceRegex.Replace(normalized, m => $"{m.Groups[1].Value}://");
        return normalized;
    }

    internal static bool TryBuildGraphSiteKey(Uri uri, out string siteKey, out string remainderPath)
    {
        siteKey = "";
        remainderPath = "";
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.Host;
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimEnd('/');
        var sitePath = TrimToSharePointSiteCollectionPath(path);
        remainderPath = RemainderAfterSite(path, sitePath);
        if (string.IsNullOrEmpty(sitePath))
        {
            sitePath = "/";
        }

        siteKey = $"{host}:{sitePath}";
        return true;
    }

    private static string SiteCollectionPath(Uri uri)
    {
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimEnd('/');
        var sitePath = TrimToSharePointSiteCollectionPath(path);
        return string.IsNullOrEmpty(sitePath) ? "" : sitePath;
    }

    private static string TrimToSharePointSiteCollectionPath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath) || absolutePath == "/")
        {
            return absolutePath;
        }

        var segments = absolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            var root = segments[0];
            if (root.Equals("sites", StringComparison.OrdinalIgnoreCase)
                || root.Equals("teams", StringComparison.OrdinalIgnoreCase))
            {
                return "/" + root + "/" + segments[1];
            }
        }

        return "/";
    }

    private static string RemainderAfterSite(string absolutePath, string sitePath)
    {
        var full = absolutePath.TrimEnd('/');
        var site = sitePath.TrimEnd('/');
        if (full.Length <= site.Length)
        {
            return "";
        }

        var remainder = full[site.Length..].Trim('/');
        if (remainder.Contains("Forms/AllItems.aspx", StringComparison.OrdinalIgnoreCase)
            || remainder.Contains("_layouts", StringComparison.OrdinalIgnoreCase)
            || remainder.Contains("SitePages", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return remainder;
    }

    private static string FolderFromQuery(Uri uri)
    {
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
        {
            return "";
        }

        var id = QueryValue(query, "id")
            ?? QueryValue(query, "RootFolder")
            ?? QueryValue(query, "rootFolder");
        if (string.IsNullOrWhiteSpace(id))
        {
            return "";
        }

        var decoded = Uri.UnescapeDataString(id).Trim('/');
        var segments = decoded.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2
            && (segments[0].Equals("sites", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("teams", StringComparison.OrdinalIgnoreCase)))
        {
            return string.Join('/', segments.Skip(2));
        }

        return decoded;
    }

    private static string NormalizeFolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        var trimmed = path.Trim().Trim('/');
        trimmed = trimmed.Replace('\\', '/');
        while (trimmed.Contains("//", StringComparison.Ordinal))
        {
            trimmed = trimmed.Replace("//", "/", StringComparison.Ordinal);
        }

        return trimmed;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static bool LooksLikeSharingLink(Uri uri) =>
        uri.AbsolutePath.Contains("/:f:/", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.Contains("/:u:/", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.Contains("/:w:/", StringComparison.OrdinalIgnoreCase);

    private static string? QueryValue(string query, string key)
    {
        var trimmed = query.TrimStart('?');
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            var name = eq < 0 ? part : part[..eq];
            if (!Uri.UnescapeDataString(name).Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return eq < 0 ? "" : part[(eq + 1)..];
        }

        return null;
    }
}
