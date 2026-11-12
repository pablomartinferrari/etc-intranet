using System.Text.Json;
using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Intranet.Api.FeatureRequests;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public sealed class AgentSourceConfirmRequiredException(AgentSourceProbeDto probe)
    : AgentSourceException(probe.Summary, 409)
{
    public AgentSourceProbeDto Probe { get; } = probe;
}

public interface IAgentSourceIngestRunner
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}

public sealed class AgentSourceService : IAgentSourceIngestRunner
{
    private readonly IntranetDbContext _db;
    private readonly ISharePointFolderGraph _graph;
    private readonly IHostedEmbeddingClient _embeddings;
    private readonly IKnowledgeDocumentUpsert _upsert;
    private readonly FeatureRequestService _featureRequests;
    private readonly KnowledgeBaseOptions _options;
    private readonly ILogger<AgentSourceService> _logger;

    public AgentSourceService(
        IntranetDbContext db,
        ISharePointFolderGraph graph,
        IHostedEmbeddingClient embeddings,
        IKnowledgeDocumentUpsert upsert,
        FeatureRequestService featureRequests,
        IOptions<KnowledgeBaseOptions> options,
        ILogger<AgentSourceService> logger)
    {
        _db = db;
        _graph = graph;
        _embeddings = embeddings;
        _upsert = upsert;
        _featureRequests = featureRequests;
        _options = options.Value;
        _logger = logger;
    }

    public AgentSourceCapabilitiesDto Capabilities()
    {
        var limits = _options.AgentSources;
        return new AgentSourceCapabilitiesDto(
            _graph.IsConfigured,
            _embeddings.IsConfigured,
            limits.SoftMaxFiles,
            limits.SoftMaxBytes,
            limits.MediumMaxFiles,
            limits.MediumMaxBytes,
            limits.MaxFileBytes,
            limits.MaxDepth);
    }

    public async Task<AgentSourceProbeDto> ProbeAsync(
        string? siteUrl,
        string? folderPath,
        CancellationToken cancellationToken)
    {
        var error = AgentSourceRequestValidator.ValidateProbe(siteUrl, folderPath);
        if (error is not null)
        {
            throw new AgentSourceException(error);
        }

        if (!SharePointFolderUrlParser.TryParse(siteUrl, folderPath, out var folder, out var parseError)
            || folder is null)
        {
            throw new AgentSourceException(parseError ?? "Could not parse the SharePoint URL.");
        }

        var raw = await _graph.ProbeAsync(folder, cancellationToken);
        return ToProbeDto(folder, raw);
    }

    public async Task<AgentSourceDto> ConnectAsync(
        AgentSourceConnectRequestDto request,
        string userOid,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var error = AgentSourceRequestValidator.ValidateConnect(request.SiteUrl, request.FolderPath, request.Label);
        if (error is not null)
        {
            throw new AgentSourceException(error);
        }

        if (!SharePointFolderUrlParser.TryParse(request.SiteUrl, request.FolderPath, out var folder, out var parseError)
            || folder is null)
        {
            throw new AgentSourceException(parseError ?? "Could not parse the SharePoint URL.");
        }

        var identity = SharePointFolderUrlParser.FolderIdentity(folder);
        var existing = await _db.AgentSources
            .Include(s => s.Jobs)
            .FirstOrDefaultAsync(
                s => s.FolderIdentity == identity && s.Status != AgentSourceStatuses.Disconnected,
                cancellationToken);
        if (existing is not null)
        {
            throw new AgentSourceException("This SharePoint folder is already connected.", 409);
        }

        var probe = ToProbeDto(folder, await _graph.ProbeAsync(folder, cancellationToken));
        if (probe.RequiresConfirm && !request.ConfirmMedium)
        {
            throw new AgentSourceConfirmRequiredException(probe);
        }

        var now = DateTimeOffset.UtcNow;
        var source = new AgentSource
        {
            Id = Guid.NewGuid(),
            CreatedByOid = userOid,
            CreatedBy = createdBy,
            Label = string.IsNullOrWhiteSpace(request.Label) ? folder.DisplayPath : request.Label.Trim(),
            SiteUrl = folder.SiteUrl,
            FolderPath = folder.FolderPath,
            FolderIdentity = identity,
            Status = probe.RequiresApproval ? AgentSourceStatuses.AwaitingApproval : AgentSourceStatuses.Connected,
            CreatedAt = now,
        };

        var job = new AgentSourceJob
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            Status = probe.RequiresApproval ? AgentSourceJobStatuses.AwaitingApproval : AgentSourceJobStatuses.Queued,
            LimitTier = probe.LimitTier,
            ConfirmedMedium = request.ConfirmMedium,
            ProbeFileCount = probe.FileCount,
            ProbeTotalBytes = probe.TotalBytes,
            ProbeMaxDepth = probe.MaxDepth,
            ProbeAllowedFiles = probe.AllowedFiles,
            ProbeAllowedBytes = probe.AllowedBytes,
            ProbeSkippedFiles = probe.SkippedFiles,
            ProbeSampleExtensionsJson = JsonSerializer.Serialize(probe.SampleExtensions),
            ProbeTruncated = probe.Truncated,
            CreatedAt = now,
            Source = source,
        };

        if (probe.RequiresApproval)
        {
            var ticket = await _featureRequests.CreateAsync(
                FeatureRequestPages.Other,
                BuildApprovalNote(folder, probe, createdBy),
                createdBy,
                cancellationToken,
                "Chat agent sources");
            source.ApprovalRequestId = ticket.Id;
        }

        source.Jobs.Add(job);
        source.LatestJobId = job.Id;
        _db.AgentSources.Add(source);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(source, job);
    }

    public async Task<IReadOnlyList<AgentSourceDto>> ListAsync(CancellationToken cancellationToken)
    {
        var sources = await _db.AgentSources
            .AsNoTracking()
            .Include(s => s.Jobs)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sources.Select(s =>
        {
            var latest = s.LatestJobId is { } id
                ? s.Jobs.FirstOrDefault(j => j.Id == id) ?? s.Jobs.MaxBy(j => j.CreatedAt)
                : s.Jobs.MaxBy(j => j.CreatedAt);
            return ToDto(s, latest);
        }).ToList();
    }

    public async Task DisconnectAsync(Guid sourceId, CancellationToken cancellationToken)
    {
        var source = await _db.AgentSources
            .Include(s => s.Documents)
            .Include(s => s.Jobs)
            .FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken)
            ?? throw new AgentSourceException("Source not found.", 404);

        if (source.Status == AgentSourceStatuses.Disconnected)
        {
            return;
        }

        source.Status = AgentSourceStatuses.Disconnected;
        source.DisconnectedAt = DateTimeOffset.UtcNow;
        var queued = source.Jobs
            .Where(j => j.Status is AgentSourceJobStatuses.Queued or AgentSourceJobStatuses.AwaitingApproval)
            .ToList();
        foreach (var job in queued)
        {
            AgentSourceJobStateMachine.Apply(job, AgentSourceJobStatuses.Failed, "Source disconnected before ingest finished.");
        }

        await _db.SaveChangesAsync(cancellationToken);

        var docIds = source.Documents.Select(d => d.DocumentId).ToList();
        try
        {
            await _upsert.MarkDocumentsInactiveAsync(docIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not mark Knowledge documents inactive for source {SourceId}.", sourceId);
        }
    }

    public async Task<AgentSourceJobDto?> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _db.AgentSources
            .AsNoTracking()
            .SelectMany(s => s.Jobs)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        return job is null ? null : ToJobDto(job);
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var job = await _db.AgentSourceJobs
            .Include(j => j.Source)
            .Where(j => j.Status == AgentSourceJobStatuses.Queued)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return false;
        }

        if (job.Source.Status != AgentSourceStatuses.Connected)
        {
            AgentSourceJobStateMachine.Apply(job, AgentSourceJobStatuses.Failed, "Source is not connected.");
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        try
        {
            AgentSourceJobStateMachine.Apply(job, AgentSourceJobStatuses.Probing);
            await _db.SaveChangesAsync(cancellationToken);

            if (!SharePointFolderUrlParser.TryParse(job.Source.SiteUrl, job.Source.FolderPath, out var folder, out var parseError)
                || folder is null)
            {
                throw new AgentSourceException(parseError ?? "Could not parse the stored SharePoint URL.");
            }

            AgentSourceJobStateMachine.Apply(job, AgentSourceJobStatuses.Running);
            await _db.SaveChangesAsync(cancellationToken);

            await IngestFolderAsync(job, folder, cancellationToken);
            AgentSourceJobStateMachine.Apply(job, AgentSourceJobStatuses.Done);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = ex is AgentSourceException agent
                ? agent.Message
                : $"Ingest failed: {ex.Message}";
            _logger.LogWarning(ex, "Agent source job {JobId} failed.", job.Id);
            try
            {
                AgentSourceJobStateMachine.Apply(job, AgentSourceJobStatuses.Failed, message);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Could not persist failure for job {JobId}.", job.Id);
            }
        }

        return true;
    }

    private async Task IngestFolderAsync(
        AgentSourceJob job,
        SharePointFolderRef folder,
        CancellationToken cancellationToken)
    {
        var pending = new List<(SharePointDriveFile File, string Text)>();
        await foreach (var file in _graph.EnumerateAllowedFilesAsync(folder, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes;
            try
            {
                bytes = await _graph.DownloadAsync(file.DriveId, file.ItemId, cancellationToken);
            }
            catch (Exception ex)
            {
                job.FilesFailed++;
                _logger.LogWarning(ex, "Download failed for {Name}", file.Name);
                continue;
            }

            var text = DocumentTextExtractor.Extract(file.Name, bytes);
            if (string.IsNullOrWhiteSpace(text))
            {
                job.FilesSkipped++;
                continue;
            }

            pending.Add((file, text));
            if (pending.Count >= 4)
            {
                await FlushBatchAsync(job, pending, cancellationToken);
                pending.Clear();
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        if (pending.Count > 0)
        {
            await FlushBatchAsync(job, pending, cancellationToken);
        }
    }

    private async Task FlushBatchAsync(
        AgentSourceJob job,
        List<(SharePointDriveFile File, string Text)> batch,
        CancellationToken cancellationToken)
    {
        var chunked = batch
            .Select(item => (item.File, Chunks: TextChunker.Chunk(item.Text)))
            .Where(item => item.Chunks.Count > 0)
            .ToList();

        var allChunks = chunked.SelectMany(item => item.Chunks).ToList();
        IReadOnlyList<float[]>? vectors = null;
        if (_embeddings.IsConfigured && allChunks.Count > 0)
        {
            var packed = new List<float[]>();
            const int embedBatch = 32;
            for (var i = 0; i < allChunks.Count; i += embedBatch)
            {
                var slice = allChunks.Skip(i).Take(embedBatch).ToList();
                var part = await _embeddings.EmbedAsync(slice, cancellationToken);
                packed.AddRange(part);
            }

            vectors = packed;
        }

        var offset = 0;
        foreach (var (file, chunks) in chunked)
        {
            IReadOnlyList<float[]>? docVectors = null;
            if (vectors is not null)
            {
                docVectors = vectors.Skip(offset).Take(chunks.Count).ToList();
            }

            offset += chunks.Count;
            try
            {
                var documentId = await _upsert.UpsertSharePointDocumentAsync(
                    job.Id,
                    file.Name,
                    file.WebUrl,
                    file.ItemId,
                    MimeFromName(file.Name),
                    job.Source.CreatedByOid,
                    chunks,
                    docVectors,
                    _embeddings.ModelName,
                    cancellationToken);
                _db.AgentSourceDocuments.Add(new AgentSourceDocument
                {
                    SourceId = job.SourceId,
                    DocumentId = documentId,
                    AddedAt = DateTimeOffset.UtcNow,
                });
                job.FilesProcessed++;
            }
            catch (Exception ex)
            {
                job.FilesFailed++;
                _logger.LogWarning(ex, "Failed to index {Name}", file.Name);
            }
        }
    }

    private AgentSourceProbeDto ToProbeDto(SharePointFolderRef folder, SharePointProbeResult raw)
    {
        var decision = AgentSourceLimitEvaluator.Evaluate(raw.AllowedFiles, raw.AllowedBytes, _options.AgentSources);
        var summary = raw.Truncated
            ? $"{decision.Summary} Probe stopped early after scanning a very large tree (max depth {_options.AgentSources.MaxDepth}, {raw.FileCount:N0} files seen)."
            : decision.Summary;
        return new AgentSourceProbeDto(
            folder.SiteUrl,
            folder.FolderPath,
            folder.DisplayPath,
            raw.FileCount,
            raw.TotalBytes,
            AgentSourceLimitEvaluator.FormatBytes(raw.TotalBytes),
            raw.AllowedFiles,
            raw.AllowedBytes,
            AgentSourceLimitEvaluator.FormatBytes(raw.AllowedBytes),
            raw.SkippedFiles,
            raw.MaxDepthReached,
            raw.SampleExtensions,
            raw.Truncated,
            decision.Tier.ToString().ToLowerInvariant(),
            decision.CanAutoRun,
            decision.RequiresConfirm,
            decision.RequiresApproval,
            summary);
    }

    private static string BuildApprovalNote(SharePointFolderRef folder, AgentSourceProbeDto probe, string createdBy) =>
        $"""
        Large SharePoint folder needs admin approval before Chat ingest.

        Site: {folder.SiteUrl}
        Folder: {folder.FolderPath}
        Requested by: {createdBy}

        Probe: {probe.AllowedFiles:N0} ingestible files ({probe.AllowedBytesLabel}), {probe.FileCount:N0} total files ({probe.TotalBytesLabel}), skipped {probe.SkippedFiles:N0}, depth {probe.MaxDepth}, extensions {string.Join(", ", probe.SampleExtensions)}.
        Truncated: {probe.Truncated}. Limit: {probe.LimitTier}.

        v1 is a one-time full ingest (no delta sync yet).
        """;

    private static AgentSourceDto ToDto(AgentSource source, AgentSourceJob? job) =>
        new(
            source.Id,
            source.Label,
            source.SiteUrl,
            source.FolderPath,
            string.IsNullOrWhiteSpace(source.FolderPath) ? source.SiteUrl : $"{source.SiteUrl}/{source.FolderPath}",
            source.Status,
            source.CreatedBy,
            source.CreatedAt,
            source.DisconnectedAt,
            source.ApprovalRequestId,
            job is null ? null : ToJobDto(job));

    private static AgentSourceJobDto ToJobDto(AgentSourceJob job)
    {
        IReadOnlyList<string> extensions = [];
        if (!string.IsNullOrWhiteSpace(job.ProbeSampleExtensionsJson))
        {
            try
            {
                extensions = JsonSerializer.Deserialize<List<string>>(job.ProbeSampleExtensionsJson) ?? [];
            }
            catch (JsonException)
            {
                extensions = [];
            }
        }

        return new AgentSourceJobDto(
            job.Id,
            job.SourceId,
            job.Status,
            job.LimitTier,
            job.ProbeAllowedFiles,
            job.ProbeAllowedBytes,
            job.ProbeSkippedFiles,
            extensions,
            job.ProbeTruncated,
            job.ErrorMessage,
            job.FilesProcessed,
            job.FilesFailed,
            job.FilesSkipped,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt);
    }

    private static string? MimeFromName(string name) =>
        AgentSourceFileRules.GetExtension(name) switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".html" or ".htm" => "text/html",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            _ => null,
        };
}
