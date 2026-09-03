using Npgsql;
using Pgvector;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public interface IKnowledgeDocumentUpsert
{
    Task<Guid> UpsertSharePointDocumentAsync(
        Guid sourceJobId,
        string title,
        string? sourceUri,
        string? externalId,
        string? mimeType,
        string? uploadedByOid,
        IReadOnlyList<string> chunks,
        IReadOnlyList<float[]>? embeddings,
        string embeddingModel,
        CancellationToken cancellationToken);

    Task MarkDocumentsInactiveAsync(IReadOnlyList<Guid> documentIds, CancellationToken cancellationToken);
}

public sealed class KnowledgeDocumentUpsert : IKnowledgeDocumentUpsert
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<KnowledgeDocumentUpsert> _logger;

    public KnowledgeDocumentUpsert(NpgsqlDataSource dataSource, ILogger<KnowledgeDocumentUpsert> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<Guid> UpsertSharePointDocumentAsync(
        Guid sourceJobId,
        string title,
        string? sourceUri,
        string? externalId,
        string? mimeType,
        string? uploadedByOid,
        IReadOnlyList<string> chunks,
        IReadOnlyList<float[]>? embeddings,
        string embeddingModel,
        CancellationToken cancellationToken)
    {
        var documentId = Guid.NewGuid();
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await using (var docCmd = new NpgsqlCommand(
            """
            INSERT INTO documents (
                id, source_type, title, source_uri, external_id, mime_type, storage_uri,
                ingest_status, ingest_detail, uploaded_by_oid, ingest_job_id, created_at, updated_at
            ) VALUES (
                @id, 'sharepoint_folder', @title, @sourceUri, @externalId, @mime, @storage,
                @status, @detail, @userOid, @jobId, NOW(), NOW()
            )
            """,
            conn,
            tx))
        {
            var hasVectors = embeddings is { Count: > 0 };
            docCmd.Parameters.AddWithValue("id", documentId);
            docCmd.Parameters.AddWithValue("title", title);
            docCmd.Parameters.AddWithValue("sourceUri", (object?)sourceUri ?? DBNull.Value);
            docCmd.Parameters.AddWithValue("externalId", (object?)externalId ?? DBNull.Value);
            docCmd.Parameters.AddWithValue("mime", (object?)mimeType ?? DBNull.Value);
            docCmd.Parameters.AddWithValue("storage", sourceUri ?? title);
            docCmd.Parameters.AddWithValue("status", "completed");
            docCmd.Parameters.AddWithValue(
                "detail",
                hasVectors
                    ? $"Indexed {chunks.Count} chunks with {embeddingModel}."
                    : $"Indexed {chunks.Count} chunks for keyword search only (hosted embeddings not configured).");
            docCmd.Parameters.AddWithValue("userOid", (object?)uploadedByOid ?? DBNull.Value);
            docCmd.Parameters.AddWithValue("jobId", sourceJobId);
            await docCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunkId = Guid.NewGuid();
            await using (var chunkCmd = new NpgsqlCommand(
                """
                INSERT INTO chunks (id, document_id, chunk_index, text, token_count, created_at)
                VALUES (@id, @documentId, @idx, @text, @tokens, NOW())
                """,
                conn,
                tx))
            {
                chunkCmd.Parameters.AddWithValue("id", chunkId);
                chunkCmd.Parameters.AddWithValue("documentId", documentId);
                chunkCmd.Parameters.AddWithValue("idx", i);
                chunkCmd.Parameters.AddWithValue("text", chunks[i]);
                chunkCmd.Parameters.AddWithValue("tokens", Math.Max(1, chunks[i].Length / 4));
                await chunkCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (embeddings is { Count: > 0 } && i < embeddings.Count && embeddings[i].Length > 0)
            {
                await using var embedCmd = new NpgsqlCommand(
                    """
                    INSERT INTO embeddings (id, chunk_id, model_name, vector, created_at)
                    VALUES (@id, @chunkId, @model, @vector, NOW())
                    """,
                    conn,
                    tx);
                embedCmd.Parameters.AddWithValue("id", Guid.NewGuid());
                embedCmd.Parameters.AddWithValue("chunkId", chunkId);
                embedCmd.Parameters.AddWithValue("model", embeddingModel);
                embedCmd.Parameters.AddWithValue("vector", new Vector(embeddings[i]));
                await embedCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await tx.CommitAsync(cancellationToken);
        _logger.LogInformation("Upserted SharePoint document {DocumentId} ({Title})", documentId, title);
        return documentId;
    }

    public async Task MarkDocumentsInactiveAsync(IReadOnlyList<Guid> documentIds, CancellationToken cancellationToken)
    {
        if (documentIds.Count == 0)
        {
            return;
        }

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE documents
            SET ingest_status = 'inactive', ingest_detail = 'Source disconnected; chunks kept but excluded from retrieval.', updated_at = NOW()
            WHERE id = ANY(@ids)
            """,
            conn);
        cmd.Parameters.AddWithValue("ids", documentIds.ToArray());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
