using System.Text;
using System.Text.RegularExpressions;
using Intranet.Api.Agentic.Data;
using Intranet.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Agentic.Repository;

public sealed record RepoSearchQuery(
    string Text,
    Guid? FolderId,
    IReadOnlySet<string> FileTypes,
    string? Modified = null,
    int Take = 20);

public sealed record RepoSearchHit(
    Guid DocumentId,
    Guid FolderId,
    string Title,
    string FileType,
    string Path,
    string Snippet,
    IReadOnlyList<string> Highlights);

/// <summary>
/// Search seam. The POC uses <see cref="LocalRepositorySearch"/>. Swap the
/// DI registration for <see cref="AzureAiSearchRepositorySearch"/> when an
/// Azure AI Search index is ready — controllers stay the same.
/// </summary>
public interface IRepositorySearch
{
    string Provider { get; }

    Task ReindexDocumentAsync(AgenticRepoDocument document, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepoSearchHit>> SearchAsync(RepoSearchQuery query, CancellationToken cancellationToken);
}

public sealed class LocalRepositorySearch : IRepositorySearch
{
    private readonly IntranetDbContext _db;

    public LocalRepositorySearch(IntranetDbContext db)
    {
        _db = db;
    }

    public string Provider => "local";

    public async Task ReindexDocumentAsync(AgenticRepoDocument document, CancellationToken cancellationToken)
    {
        var existing = await _db.AgenticRepoChunks
            .Where(c => c.DocumentId == document.Id)
            .ToListAsync(cancellationToken);
        _db.AgenticRepoChunks.RemoveRange(existing);

        var text = document.ExtractedText ?? "";
        var chunks = Chunk(text);
        for (var i = 0; i < chunks.Count; i++)
        {
            _db.AgenticRepoChunks.Add(new AgenticRepoChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                ChunkIndex = i,
                Text = chunks[i],
            });
        }

        document.IsIndexed = chunks.Count > 0;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RepoSearchHit>> SearchAsync(RepoSearchQuery query, CancellationToken cancellationToken)
    {
        var terms = Tokenize(query.Text);
        if (terms.Count == 0)
        {
            return [];
        }

        var folderIds = query.FolderId is { } folderId
            ? await CollectDescendantFolderIds(folderId, cancellationToken)
            : null;

        var documents = await _db.AgenticRepoDocuments
            .AsNoTracking()
            .Include(d => d.Folder)
            .Include(d => d.Chunks)
            .Where(d => d.IsIndexed)
            .Where(d => folderIds == null || folderIds.Contains(d.FolderId))
            .Where(d => query.FileTypes.Count == 0 || query.FileTypes.Contains(d.FileType))
            .ToListAsync(cancellationToken);

        var hits = new List<(int Score, RepoSearchHit Hit)>();
        foreach (var document in documents)
        {
            if (!MatchesModified(document.ModifiedAt, query.Modified))
            {
                continue;
            }

            var bestSnippet = "";
            var score = 0;
            foreach (var chunk in document.Chunks.OrderBy(c => c.ChunkIndex))
            {
                var chunkScore = Score(chunk.Text, terms);
                if (chunkScore > score)
                {
                    score = chunkScore;
                    bestSnippet = HighlightSnippet(chunk.Text, terms);
                }
            }

            var titleScore = Score(document.Name, terms) * 3;
            score += titleScore;
            if (score <= 0)
            {
                continue;
            }

            var path = document.Folder.Path.TrimEnd('/') + "/" + document.Name;
            hits.Add((score, new RepoSearchHit(
                document.Id,
                document.FolderId,
                document.Name,
                document.FileType,
                path.TrimStart('/'),
                bestSnippet,
                terms)));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Hit.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(query.Take, 1, 50))
            .Select(h => h.Hit)
            .ToList();
    }

    internal static List<string> Tokenize(string text)
    {
        var terms = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(text ?? "", @"[\p{L}\p{N}']+"))
        {
            var term = match.Value.ToLowerInvariant();
            if (term.Length < 2 || !seen.Add(term))
            {
                continue;
            }

            terms.Add(term);
        }

        return terms;
    }

    internal static List<string> Chunk(string text, int size = 500)
    {
        var normalized = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        var chunks = new List<string>();
        for (var i = 0; i < normalized.Length; i += size)
        {
            var len = Math.Min(size, normalized.Length - i);
            chunks.Add(normalized.Substring(i, len));
        }

        return chunks;
    }

    internal static int Score(string text, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrWhiteSpace(text) || terms.Count == 0)
        {
            return 0;
        }

        var hay = text.ToLowerInvariant();
        var score = 0;
        foreach (var term in terms)
        {
            var start = 0;
            while (start < hay.Length)
            {
                var at = hay.IndexOf(term, start, StringComparison.Ordinal);
                if (at < 0)
                {
                    break;
                }

                score++;
                start = at + term.Length;
            }
        }

        return score;
    }

    internal static string HighlightSnippet(string text, IReadOnlyList<string> terms, int radius = 90)
    {
        var hay = text ?? "";
        var lower = hay.ToLowerInvariant();
        var first = -1;
        foreach (var term in terms)
        {
            var at = lower.IndexOf(term, StringComparison.Ordinal);
            if (at >= 0 && (first < 0 || at < first))
            {
                first = at;
            }
        }

        if (first < 0)
        {
            return hay.Length <= 220 ? hay : hay[..220] + "…";
        }

        var start = Math.Max(0, first - radius);
        var end = Math.Min(hay.Length, first + radius);
        var slice = hay[start..end];
        var prefix = start > 0 ? "…" : "";
        var suffix = end < hay.Length ? "…" : "";
        return prefix + slice + suffix;
    }

    private async Task<HashSet<Guid>> CollectDescendantFolderIds(Guid folderId, CancellationToken cancellationToken)
    {
        var folders = await _db.AgenticRepoFolders.AsNoTracking().ToListAsync(cancellationToken);
        var ids = new HashSet<Guid> { folderId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var folder in folders)
            {
                if (folder.ParentId is { } parent && ids.Contains(parent) && ids.Add(folder.Id))
                {
                    changed = true;
                }
            }
        }

        return ids;
    }

    private static bool MatchesModified(DateTimeOffset modified, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter is "anytime")
        {
            return true;
        }

        var days = filter switch
        {
            "7d" => 7,
            "30d" => 30,
            "90d" => 90,
            _ => 0,
        };
        return days == 0 || modified >= DateTimeOffset.UtcNow.AddDays(-days);
    }
}

/// <summary>
/// Future Azure AI Search adapter. Registered only when
/// <c>Features:AgenticWorkspace:SearchProvider=azure</c>. The POC keeps the
/// local index as the default so no Azure resource is required.
/// </summary>
public sealed class AzureAiSearchRepositorySearch : IRepositorySearch
{
    private readonly AgenticOptions _options;
    private readonly LocalRepositorySearch _fallback;

    public AzureAiSearchRepositorySearch(IOptions<AgenticOptions> options, LocalRepositorySearch fallback)
    {
        _options = options.Value;
        _fallback = fallback;
    }

    public string Provider => "azure";

    public Task ReindexDocumentAsync(AgenticRepoDocument document, CancellationToken cancellationToken) =>
        _fallback.ReindexDocumentAsync(document, cancellationToken);

    public Task<IReadOnlyList<RepoSearchHit>> SearchAsync(RepoSearchQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AzureSearchEndpoint)
            || string.IsNullOrWhiteSpace(_options.AzureSearchApiKey))
        {
            return _fallback.SearchAsync(query, cancellationToken);
        }

        // Seam: replace this fallback with Azure.Search.Documents when an index exists.
        return _fallback.SearchAsync(query, cancellationToken);
    }
}

public static class RepositoryTextExtractor
{
    public static string DetectFileType(string fileName, string? mime)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".xls" or ".xlsx" or ".csv")
        {
            return "spreadsheet";
        }

        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" || (mime?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return "image";
        }

        return "document";
    }

    public static string Extract(string fileName, string mime, Stream content)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".txt" or ".md" or ".csv" || mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return reader.ReadToEnd();
        }

        if (ext == ".pdf" || mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                content.Position = 0;
                using var doc = UglyToad.PdfPig.PdfDocument.Open(content);
                var builder = new StringBuilder();
                foreach (var page in doc.GetPages())
                {
                    builder.AppendLine(page.Text);
                }

                return builder.ToString();
            }
            catch
            {
                return "";
            }
        }

        return "";
    }
}
