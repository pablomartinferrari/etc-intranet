using System.Diagnostics;
using System.Text.Json;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Intranet.Api.KnowledgeBase.Services;

public sealed class IngestService
{
    private readonly KnowledgeBaseOptions _options;
    private readonly KnowledgeUploadStaging _staging;
    private readonly ILogger<IngestService> _logger;
    private readonly IWebHostEnvironment _env;

    public IngestService(
        IOptions<KnowledgeBaseOptions> options,
        KnowledgeUploadStaging staging,
        ILogger<IngestService> logger,
        IWebHostEnvironment env)
    {
        _options = options.Value;
        _staging = staging;
        _logger = logger;
        _env = env;
    }

    public async Task<IngestUploadEnqueueDto> EnqueueUploadAsync(
        IFormFile file,
        string? userOid,
        Guid? projectId,
        CancellationToken cancellationToken = default)
    {
        var documentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var safeName = Path.GetFileName(file.FileName);
        var mime = file.ContentType;

        await using var uploadStream = file.OpenReadStream();
        var staged = await _staging.StageAsync(documentId, safeName, uploadStream, cancellationToken);
        var payload = JsonSerializer.Serialize(staged.JobPayload);

        await using var conn = new NpgsqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using (var docCmd = new NpgsqlCommand(
            """
            INSERT INTO documents (
                id, source_type, title, source_uri, mime_type, storage_uri,
                ingest_status, uploaded_by_oid, ingest_job_id, created_at, updated_at
            ) VALUES (
                @id, 'upload', @title, @title, @mime, @storage, 'queued',
                @userOid, @jobId, NOW(), NOW()
            )
            """,
            conn))
        {
            docCmd.Parameters.AddWithValue("id", documentId);
            docCmd.Parameters.AddWithValue("title", safeName);
            docCmd.Parameters.AddWithValue("mime", (object?)mime ?? DBNull.Value);
            docCmd.Parameters.AddWithValue("storage", staged.StorageUri);
            docCmd.Parameters.AddWithValue("userOid", (object?)userOid ?? DBNull.Value);
            docCmd.Parameters.AddWithValue("jobId", jobId);
            await docCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (projectId.HasValue)
        {
            await using var linkCmd = new NpgsqlCommand(
                """
                INSERT INTO kb_project_documents (project_id, document_id)
                VALUES (@projectId, @documentId)
                ON CONFLICT DO NOTHING
                """,
                conn);
            linkCmd.Parameters.AddWithValue("projectId", projectId.Value);
            linkCmd.Parameters.AddWithValue("documentId", documentId);
            await linkCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var jobCmd = new NpgsqlCommand(
            """
            INSERT INTO ingest_jobs (id, job_type, payload, status)
            VALUES (@id, 'staged_file', @payload::jsonb, 'pending')
            """,
            conn))
        {
            jobCmd.Parameters.AddWithValue("id", jobId);
            jobCmd.Parameters.AddWithValue("payload", payload);
            await jobCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Enqueued ingest job {JobId} for document {DocumentId} ({FileName})",
            jobId,
            documentId,
            safeName);

        var hint = _staging.UsesAzureBlob
            ? "File uploaded to cloud storage. Indexing starts when the ingest worker is running on the GPU VM."
            : "File uploaded. Indexing will continue in the background — start the ingest worker if it is not already running.";

        return new IngestUploadEnqueueDto(documentId, jobId, "queued", hint);
    }

    public Task<IngestUploadResponseDto> IngestLocalFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        return RunCliAsync($"ingest local --path \"{folderPath}\"", cancellationToken);
    }

    public Task<IngestUploadResponseDto> IngestSharePointAsync(
        IngestSharePointRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "ingest sharepoint", $"--site \"{request.SiteUrl}\"" };
        if (!string.IsNullOrWhiteSpace(request.FolderPath))
        {
            args.Add($"--folder \"{request.FolderPath}\"");
        }

        if (!string.IsNullOrWhiteSpace(request.FilePath))
        {
            args.Add($"--file \"{request.FilePath}\"");
        }

        return RunCliAsync(string.Join(' ', args), cancellationToken);
    }

    public async Task<IngestJobEnqueueResponseDto> EnqueueSharePointDeltaAsync(
        IngestSharePointRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            site_url = request.SiteUrl,
            folder_path = request.FolderPath ?? string.Empty,
        });

        await using var conn = new NpgsqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO ingest_jobs (id, job_type, payload, status)
            VALUES (@id, 'sharepoint_delta', @payload::jsonb, 'pending')
            """,
            conn);
        cmd.Parameters.AddWithValue("id", jobId);
        cmd.Parameters.AddWithValue("payload", payload);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new IngestJobEnqueueResponseDto(jobId, "pending");
    }

    public async Task<IngestJobStatusDto?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, job_type, status, error_message, created_at, started_at, finished_at
            FROM ingest_jobs WHERE id = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("id", jobId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new IngestJobStatusDto(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
    }

    private async Task<IngestUploadResponseDto> RunCliAsync(string arguments, CancellationToken cancellationToken)
    {
        var etcKgPath = ResolvePath(_options.EtcKgPath);
        var cliModule = Path.Combine(etcKgPath, "ingest", "cli.py");
        if (!File.Exists(cliModule))
        {
            throw new InvalidOperationException($"etc-kg CLI not found at {cliModule}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = _options.PythonPath,
            Arguments = $"-m ingest.cli {arguments}",
            WorkingDirectory = etcKgPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _logger.LogInformation("Running etc-kg: {File} {Args}", psi.FileName, psi.Arguments);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start etc-kg process.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogError("etc-kg failed: {Stderr}", stderr);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        }

        _logger.LogDebug("etc-kg output: {Stdout}", stdout);

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            return new IngestUploadResponseDto(
                root.TryGetProperty("run_id", out var runId) && runId.ValueKind == JsonValueKind.String
                    ? Guid.Parse(runId.GetString()!)
                    : null,
                root.GetProperty("processed").GetInt32(),
                root.GetProperty("failed").GetInt32(),
                root.GetProperty("status").GetString() ?? "unknown",
                root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array
                    ? errors.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList()
                    : []);
        }
        catch (JsonException)
        {
            return new IngestUploadResponseDto(null, 0, 0, "completed", []);
        }
    }

    private string ResolvePath(string path) =>
        Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, path));
}
