using System.Text.RegularExpressions;

namespace Intranet.Api.Agentic.Repository;

public static class RepositoryNamedDocuments
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "this", "that", "pdf", "docx", "xlsx", "pptx", "txt",
    };

    public static bool NamesDocument(string question, string fileName)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var full = fileName.Trim();
        if (question.Contains(full, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var stem = Path.GetFileNameWithoutExtension(full);
        if (stem.Length >= 3 && question.Contains(stem, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var questionTerms = new HashSet<string>(LocalRepositorySearch.Tokenize(question), StringComparer.OrdinalIgnoreCase);
        var nameTerms = LocalRepositorySearch.Tokenize(stem)
            .Where(t => t.Length >= 3 && !Stopwords.Contains(t))
            .ToList();
        if (nameTerms.Count >= 2)
        {
            return nameTerms.All(questionTerms.Contains);
        }

        return nameTerms.Count == 1 && nameTerms[0].Length >= 6 && questionTerms.Contains(nameTerms[0]);
    }

    public static IReadOnlyList<string> DistinctiveNameTerms(string fileName) =>
        LocalRepositorySearch.Tokenize(Path.GetFileNameWithoutExtension(fileName) ?? "")
            .Where(t => t.Length >= 3 && !Stopwords.Contains(t))
            .ToList();

    public static string NormalizeName(string fileName) =>
        Regex.Replace(fileName ?? "", @"\s+", " ").Trim();
}
