using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;

namespace Intranet.Api.KnowledgeBase.Services;

public sealed class SemanticSearchService
{
    private readonly KnowledgeBaseOptions _options;
    private readonly OllamaClient _ollama;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        IOptions<KnowledgeBaseOptions> options,
        OllamaClient ollama,
        NpgsqlDataSource dataSource,
        ILogger<SemanticSearchService> logger)
    {
        _options = options.Value;
        _ollama = ollama;
        _dataSource = dataSource;
        _logger = logger;
    }

    public static bool IsSearchIntent(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var lower = trimmed.ToLowerInvariant();
        string[] searchPrefixes =
        [
            "find ",
            "find me ",
            "show me ",
            "locate ",
            "search for ",
            "search ",
            "list ",
            "get me ",
        ];

        return searchPrefixes.Any(lower.StartsWith);
    }

    public async Task<SearchResponseDto> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var phrases = SplitOrPhrases(request.Query);
        var aggregated = new Dictionary<Guid, SearchResultItemDto>();

        foreach (var phrase in phrases)
        {
            var phraseResults = await SearchPhraseAsync(phrase, request.DocType, request.Limit, cancellationToken);
            foreach (var item in phraseResults)
            {
                if (!aggregated.TryGetValue(item.DocumentId, out var existing) || item.Score > existing.Score)
                {
                    aggregated[item.DocumentId] = item;
                }
            }
        }

        var results = aggregated.Values
            .OrderByDescending(r => r.Score)
            .Take(request.Limit)
            .ToList();

        return new SearchResponseDto("search", results);
    }

    public async Task<IReadOnlyList<(Guid ChunkId, Guid DocumentId, string Title, string? SourceUri, string Text, double Score)>>
        RetrieveChunksAsync(
            string query,
            int topK,
            Guid? documentId = null,
            Guid? projectId = null,
            CancellationToken cancellationToken = default)
    {
        var vector = await _ollama.EmbedAsync(query, cancellationToken);
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        var sql = """
            SELECT c.id, d.id, d.title, d.source_uri, c.text,
                   (1 - (e.vector <=> @queryVector)) AS vector_score,
                   COALESCE(ts_rank(c.search_vector, plainto_tsquery('english', @queryText)), 0) AS keyword_score
            FROM embeddings e
            JOIN chunks c ON c.id = e.chunk_id
            JOIN documents d ON d.id = c.document_id
            """ + (projectId.HasValue
                ? " JOIN kb_project_documents pd ON pd.document_id = d.id AND pd.project_id = @projectId "
                : "") + """
            WHERE d.ingest_status = 'completed'
            """ + (documentId.HasValue ? " AND d.id = @documentId" : "") + """
            
            ORDER BY ((1 - (e.vector <=> @queryVector)) * @vectorWeight
                      + COALESCE(ts_rank(c.search_vector, plainto_tsquery('english', @queryText)), 0) * @keywordWeight) DESC
            LIMIT @topK
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("queryVector", new Vector(vector));
        cmd.Parameters.AddWithValue("queryText", query);
        cmd.Parameters.AddWithValue("vectorWeight", 1.0 - _options.HybridKeywordWeight);
        cmd.Parameters.AddWithValue("keywordWeight", _options.HybridKeywordWeight);
        cmd.Parameters.AddWithValue("topK", topK);
        if (documentId.HasValue)
        {
            cmd.Parameters.AddWithValue("documentId", documentId.Value);
        }

        if (projectId.HasValue)
        {
            cmd.Parameters.AddWithValue("projectId", projectId.Value);
        }

        var results = new List<(Guid, Guid, string, string?, string, double)>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var vectorScore = reader.GetDouble(5);
            var keywordScore = reader.GetDouble(6);
            var combined = vectorScore * (1.0 - _options.HybridKeywordWeight) + keywordScore * _options.HybridKeywordWeight;
            results.Add((
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                combined));
        }

        return results;
    }

    private static IEnumerable<string> SplitOrPhrases(string query)
    {
        var parts = query.Split(" or ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? [query.Trim()] : parts;
    }

    private async Task<IReadOnlyList<SearchResultItemDto>> SearchPhraseAsync(
        string phrase,
        string? docType,
        int limit,
        CancellationToken cancellationToken)
    {
        var vector = await _ollama.EmbedAsync(phrase, cancellationToken);
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        var sql = """
            SELECT d.id, d.title, d.source_uri, d.doc_type, d.source_type,
                   MAX((1 - (e.vector <=> @queryVector)) * @vectorWeight
                       + COALESCE(ts_rank(c.search_vector, plainto_tsquery('english', @queryText)), 0) * @keywordWeight) AS score,
                   (array_agg(c.text ORDER BY e.vector <=> @queryVector))[1] AS snippet
            FROM embeddings e
            JOIN chunks c ON c.id = e.chunk_id
            JOIN documents d ON d.id = c.document_id
            WHERE d.ingest_status = 'completed'
            """ + (string.IsNullOrWhiteSpace(docType) ? "" : " AND d.doc_type = @docType") + """
            
            GROUP BY d.id, d.title, d.source_uri, d.doc_type, d.source_type
            ORDER BY score DESC
            LIMIT @limit
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("queryVector", new Vector(vector));
        cmd.Parameters.AddWithValue("queryText", phrase);
        cmd.Parameters.AddWithValue("vectorWeight", 1.0 - _options.HybridKeywordWeight);
        cmd.Parameters.AddWithValue("keywordWeight", _options.HybridKeywordWeight);
        cmd.Parameters.AddWithValue("limit", limit);
        if (!string.IsNullOrWhiteSpace(docType))
        {
            cmd.Parameters.AddWithValue("docType", docType);
        }

        var results = new List<SearchResultItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SearchResultItemDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetDouble(5),
                reader.IsDBNull(6) ? string.Empty : reader.GetString(6)));
        }

        return results;
    }
}
