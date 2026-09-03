using Intranet.Api.KnowledgeBase.Options;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public static class AgentSourceRequestValidator
{
    public const int LabelMaxLength = 200;
    public const int UrlMaxLength = 2000;
    public const int FolderPathMaxLength = 1000;

    public static string? ValidateProbe(string? siteUrl, string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            return "Paste a SharePoint site URL.";
        }

        if (siteUrl.Trim().Length > UrlMaxLength)
        {
            return $"Keep the site URL under {UrlMaxLength} characters.";
        }

        if (folderPath is { Length: > FolderPathMaxLength })
        {
            return $"Keep the folder path under {FolderPathMaxLength} characters.";
        }

        if (!SharePointFolderUrlParser.TryParse(siteUrl, folderPath, out _, out var parseError))
        {
            return parseError;
        }

        return null;
    }

    public static string? ValidateConnect(string? siteUrl, string? folderPath, string? label)
    {
        var probeError = ValidateProbe(siteUrl, folderPath);
        if (probeError is not null)
        {
            return probeError;
        }

        if (label is { Length: > LabelMaxLength })
        {
            return $"Keep the label under {LabelMaxLength} characters.";
        }

        return null;
    }

    public static AgentSourceOptions Snapshot(AgentSourceOptions? options) =>
        options ?? new AgentSourceOptions();
}
