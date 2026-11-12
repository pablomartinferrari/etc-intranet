namespace Intranet.Api.Agentic.Repository;

public static class RepositoryNames
{
    public const int FolderMaxLength = 200;
    public const int FileMaxLength = 260;

    private static readonly char[] InvalidChars =
        Path.GetInvalidFileNameChars()
            .Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])
            .Distinct()
            .ToArray();

    public static string NormalizeFolderName(string? name) =>
        Normalize(name, FolderMaxLength);

    public static string NormalizeFileName(string? name)
    {
        var fileName = Path.GetFileName((name ?? "").Trim());
        return Normalize(fileName, FileMaxLength);
    }

    public const string PathSeparator = " / ";

    public static string CombinePath(string parentPath, string name)
    {
        var parent = string.IsNullOrWhiteSpace(parentPath) ? "Root" : parentPath.Trim();
        return parent + PathSeparator + name;
    }

    /// <summary>
    /// Materialized-path subtree check: a folder is in scope when its path equals
    /// the scope or is a descendant (<c>scope / …</c>). Used for SQL prefix filters
    /// (<c>Path = scope OR Path LIKE scope + ' / %'</c>) so search does not walk a
    /// folder-id closure in memory.
    /// </summary>
    public static bool IsInSubtree(string folderPath, string scopePath)
    {
        if (string.IsNullOrWhiteSpace(scopePath))
        {
            return true;
        }

        var path = (folderPath ?? "").Trim();
        var scope = scopePath.Trim();
        return path.Equals(scope, StringComparison.Ordinal)
            || path.StartsWith(scope + PathSeparator, StringComparison.Ordinal);
    }

    public static string PrefixFilter(string scopePath) => scopePath.Trim() + PathSeparator;

    public static string DocumentPath(string folderPath, string fileName) =>
        CombinePath(folderPath, fileName);

    public static IReadOnlyList<string> PathSegments(string path) =>
        path.Split(" / ", StringSplitOptions.None)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    private static string Normalize(string? name, int maxLength)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new AgenticException("A name is required.", StatusCodes.Status400BadRequest);
        }

        if (trimmed is "." or "..")
        {
            throw new AgenticException("That name is not allowed.", StatusCodes.Status400BadRequest);
        }

        if (trimmed.Contains("..", StringComparison.Ordinal)
            || trimmed.Contains('/')
            || trimmed.Contains('\\'))
        {
            throw new AgenticException("Names cannot contain path separators or '..'.", StatusCodes.Status400BadRequest);
        }

        if (trimmed.IndexOfAny(InvalidChars) >= 0)
        {
            throw new AgenticException(
                "Names cannot contain reserved characters such as < > : \" | ? *.",
                StatusCodes.Status400BadRequest);
        }

        if (trimmed.Length > maxLength)
        {
            throw new AgenticException($"Names cannot be longer than {maxLength} characters.", StatusCodes.Status400BadRequest);
        }

        return trimmed;
    }
}
