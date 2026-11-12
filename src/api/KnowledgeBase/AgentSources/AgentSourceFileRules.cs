using Intranet.Api.KnowledgeBase.Options;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public static class AgentSourceFileRules
{
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".pptx", ".xlsx",
        ".txt", ".md", ".csv", ".rtf",
        ".html", ".htm", ".json", ".xml",
        ".odt", ".ods", ".odp",
    };

    public static readonly HashSet<string> SkippedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".mpeg", ".mpg", ".m4v", ".3gp",
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a",
        ".iso", ".img", ".vhd", ".vhdx", ".dmg", ".vmdk",
        ".exe", ".dll", ".bin", ".so", ".dylib", ".msi", ".apk", ".class",
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tgz",
        ".doc", ".ppt", ".xls",
    };

    public static bool IsAllowedExtension(string? fileName)
    {
        var ext = GetExtension(fileName);
        return ext.Length > 0 && AllowedExtensions.Contains(ext);
    }

    public static bool IsJunkExtension(string? fileName)
    {
        var ext = GetExtension(fileName);
        return ext.Length > 0 && SkippedExtensions.Contains(ext);
    }

    public static bool IsWithinSizeCap(long sizeBytes, AgentSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return sizeBytes >= 0 && sizeBytes <= options.MaxFileBytes;
    }

    public static bool ShouldIngest(string? fileName, long sizeBytes, AgentSourceOptions options) =>
        IsAllowedExtension(fileName) && IsWithinSizeCap(sizeBytes, options);

    public static string SkipReason(string? fileName, long sizeBytes, AgentSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (IsJunkExtension(fileName) || !IsAllowedExtension(fileName))
        {
            var ext = GetExtension(fileName);
            return string.IsNullOrEmpty(ext)
                ? "Skipped: file type is not a readable document."
                : $"Skipped: {ext} files are not ingested.";
        }

        if (sizeBytes > options.MaxFileBytes)
        {
            return $"Skipped: file is larger than {AgentSourceLimitEvaluator.FormatBytes(options.MaxFileBytes)}.";
        }

        return "Skipped.";
    }

    public static string GetExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return Path.GetExtension(fileName.Trim()).ToLowerInvariant();
    }
}
