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
    IReadOnlyList<string> Highlights,
    int Score = 0);

public sealed record RepoDocumentContext(
    Guid DocumentId,
    Guid FolderId,
    string Title,
    string Path,
    IReadOnlyList<string> Chunks,
    string? ExtractedText,
    string ExtractionStatus,
    string? ExtractionNote,
    bool NamedOrAttached);

/// <summary>
/// Search seam. The POC uses <see cref="LocalRepositorySearch"/>. Swap the
/// DI registration for <see cref="AzureAiSearchRepositorySearch"/> when an
/// Azure AI Search index is ready — controllers stay the same.
/// </summary>
public interface IRepositorySearch
{
    string Provider { get; }

    Task ReindexDocumentAsync(AgenticRepoDocument document, CancellationToken cancellationToken);

    Task RemoveDocumentAsync(Guid documentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepoSearchHit>> SearchAsync(RepoSearchQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepoDocumentContext>> GetDocumentsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RepoDocumentContext>> FindNamedDocumentsAsync(
        string question,
        Guid? folderId,
        CancellationToken cancellationToken);
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
        if (chunks.Count == 0 && document.ExtractionStatus == RepositoryExtractionStatus.Ok)
        {
            document.ExtractionStatus = RepositoryExtractionStatus.Empty;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var existing = await _db.AgenticRepoChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken);
        _db.AgenticRepoChunks.RemoveRange(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RepoSearchHit>> SearchAsync(RepoSearchQuery query, CancellationToken cancellationToken)
    {
        var terms = Tokenize(query.Text);
        if (terms.Count == 0)
        {
            return [];
        }

        string? scopePath = null;
        if (query.FolderId is { } folderId)
        {
            scopePath = await _db.AgenticRepoFolders.AsNoTracking()
                .Where(f => f.Id == folderId)
                .Select(f => f.Path)
                .FirstOrDefaultAsync(cancellationToken);
            if (scopePath is null)
            {
                return [];
            }
        }

        var prefix = scopePath is null ? null : RepositoryNames.PrefixFilter(scopePath);
        var documents = await _db.AgenticRepoDocuments
            .AsNoTracking()
            .Include(d => d.Folder)
            .Include(d => d.Chunks)
            .Where(d => d.IsIndexed)
            .Where(d =>
                scopePath == null
                || d.FolderPath == scopePath
                || (prefix != null && d.FolderPath.StartsWith(prefix))
                || (d.FolderPath == "" && (d.Folder.Path == scopePath || (prefix != null && d.Folder.Path.StartsWith(prefix)))))
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
                    bestSnippet = chunk.Text;
                }
            }

            var titleScore = Score(document.Name, terms) * 3;
            score += titleScore;
            if (score <= 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(bestSnippet))
            {
                bestSnippet = document.Chunks.OrderBy(c => c.ChunkIndex).Select(c => c.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
                    ?? document.ExtractedText
                    ?? "";
            }

            var folderPath = string.IsNullOrWhiteSpace(document.FolderPath) ? document.Folder.Path : document.FolderPath;
            hits.Add((score, new RepoSearchHit(
                document.Id,
                document.FolderId,
                document.Name,
                document.FileType,
                RepositoryNames.DocumentPath(folderPath, document.Name),
                bestSnippet,
                terms,
                score)));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Hit.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(query.Take, 1, 50))
            .Select(h => h.Hit)
            .ToList();
    }

    public Task<IReadOnlyList<RepoDocumentContext>> GetDocumentsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken) =>
        LoadContextsAsync(documentIds, namedOrAttached: true, cancellationToken);

    public async Task<IReadOnlyList<RepoDocumentContext>> FindNamedDocumentsAsync(
        string question,
        Guid? folderId,
        CancellationToken cancellationToken)
    {
        _ = folderId;
        if (string.IsNullOrWhiteSpace(question))
        {
            return [];
        }

        // Named files are the folder-scope exception: match by title/filename
        // across the whole repository, even when the chat is in a folder.
        var names = await _db.AgenticRepoDocuments
            .AsNoTracking()
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(cancellationToken);
        var matched = names
            .Where(d => RepositoryNamedDocuments.NamesDocument(question, d.Name))
            .Select(d => d.Id)
            .ToList();
        return await LoadContextsAsync(matched, namedOrAttached: true, cancellationToken);
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

    internal static List<string> Chunk(string text, int size = 1800, int overlap = 200)
    {
        var normalized = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        if (size <= 0)
        {
            size = 1800;
        }

        overlap = Math.Clamp(overlap, 0, Math.Max(0, size - 1));
        if (normalized.Length <= size)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var start = 0;
        while (start < normalized.Length)
        {
            var length = Math.Min(size, normalized.Length - start);
            var end = start + length;
            if (end < normalized.Length)
            {
                var window = Math.Min(400, length);
                var breakAt = normalized.LastIndexOfAny([' ', '.', ';', '\n'], end - 1, window);
                if (breakAt > start + size / 2)
                {
                    end = breakAt + 1;
                }
            }

            var slice = normalized[start..end].Trim();
            if (slice.Length > 0)
            {
                chunks.Add(slice);
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(end - overlap, start + 1);
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

    public static bool IsWeak(IReadOnlyList<RepoSearchHit> hits) =>
        hits.Count == 0 || hits.All(h => h.Score < WeakScoreThreshold);

    public const int WeakScoreThreshold = 2;

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

    private async Task<IReadOnlyList<RepoDocumentContext>> LoadContextsAsync(
        IReadOnlyCollection<Guid> documentIds,
        bool namedOrAttached,
        CancellationToken cancellationToken)
    {
        if (documentIds.Count == 0)
        {
            return [];
        }

        var ids = documentIds.Distinct().ToList();
        var documents = await _db.AgenticRepoDocuments
            .AsNoTracking()
            .Include(d => d.Folder)
            .Include(d => d.Chunks)
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(cancellationToken);
        return documents.Select(d => ToContext(d, namedOrAttached)).ToList();
    }

    internal static RepoDocumentContext ToContext(AgenticRepoDocument document, bool namedOrAttached)
    {
        var folderPath = string.IsNullOrWhiteSpace(document.FolderPath) ? document.Folder?.Path ?? "" : document.FolderPath;
        var chunks = document.Chunks.OrderBy(c => c.ChunkIndex).Select(c => c.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            chunks = Chunk(document.ExtractedText);
        }

        return new RepoDocumentContext(
            document.Id,
            document.FolderId,
            document.Name,
            RepositoryNames.DocumentPath(folderPath, document.Name),
            chunks,
            document.ExtractedText,
            string.IsNullOrWhiteSpace(document.ExtractionStatus) ? RepositoryExtractionStatus.Pending : document.ExtractionStatus,
            document.ExtractionNote,
            namedOrAttached);
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

    public Task RemoveDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        _fallback.RemoveDocumentAsync(documentId, cancellationToken);

    public Task<IReadOnlyList<RepoDocumentContext>> GetDocumentsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken) =>
        _fallback.GetDocumentsAsync(documentIds, cancellationToken);

    public Task<IReadOnlyList<RepoDocumentContext>> FindNamedDocumentsAsync(
        string question,
        Guid? folderId,
        CancellationToken cancellationToken) =>
        _fallback.FindNamedDocumentsAsync(question, folderId, cancellationToken);

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
