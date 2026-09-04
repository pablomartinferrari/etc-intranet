using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.KnowledgeBase.Controllers;

[ApiController]
[Route("api/kb")]
public sealed class KnowledgeIngestController : ControllerBase
{
    private readonly IngestService _ingest;
    private readonly KnowledgeDbContext _db;
    private readonly IKbProjectAccessService _access;

    public KnowledgeIngestController(
        IngestService ingest,
        KnowledgeDbContext db,
        IKbProjectAccessService access)
    {
        _ingest = ingest;
        _db = db;
        _access = access;
    }

    [HttpPost("ingest/upload")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 600_000_000)]
    public async Task<ActionResult<IngestUploadEnqueueDto>> Upload(
        IFormFile file,
        [FromQuery] Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        if (!projectId.HasValue)
        {
            return BadRequest("projectId is required. Upload files within a project.");
        }

        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        try
        {
            await _access.RequireAsync(projectId.Value, userOid, KbProjectPermission.Edit, cancellationToken);
        }
        catch (KbProjectAccessException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }

        var result = await _ingest.EnqueueUploadAsync(file, userOid, projectId, cancellationToken);
        return Accepted(result);
    }

    [HttpPost("ingest/sharepoint")]
    public async Task<ActionResult<IngestUploadResponseDto>> IngestSharePoint(
        [FromBody] IngestSharePointRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SiteUrl))
        {
            return BadRequest("SiteUrl is required.");
        }

        var result = await _ingest.IngestSharePointAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ingest/sharepoint/delta")]
    public async Task<ActionResult<IngestJobEnqueueResponseDto>> EnqueueSharePointDelta(
        [FromBody] IngestSharePointRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SiteUrl))
        {
            return BadRequest("SiteUrl is required.");
        }

        var result = await _ingest.EnqueueSharePointDeltaAsync(request, cancellationToken);
        return Accepted(result);
    }

    [HttpGet("ingest/jobs/{id:guid}")]
    public async Task<ActionResult<IngestJobStatusDto>> GetIngestJob(
        Guid id,
        CancellationToken cancellationToken)
    {
        var job = await _ingest.GetJobStatusAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("documents")]
    public async Task<ActionResult<IReadOnlyList<DocumentListItemDto>>> ListDocuments(
        [FromQuery] Guid? projectId,
        CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        if (!projectId.HasValue)
        {
            return BadRequest("projectId is required.");
        }

        try
        {
            await _access.RequireAsync(projectId.Value, userOid, KbProjectPermission.View, cancellationToken);
        }
        catch (KbProjectAccessException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }

        var query = _db.Documents.Where(d => _db.ProjectDocuments.Any(
            pd => pd.ProjectId == projectId.Value && pd.DocumentId == d.Id));

        var docs = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(200)
            .Select(d => new DocumentListItemDto(
                d.Id,
                d.Title,
                d.SourceType,
                d.DocType,
                d.IngestStatus,
                d.IngestDetail,
                d.CreatedAt,
                projectId))
            .ToListAsync(cancellationToken);

        return Ok(docs);
    }

    [HttpGet("ingest/runs/{id:guid}")]
    public async Task<ActionResult<IngestRunStatusDto>> GetIngestRun(
        Guid id,
        CancellationToken cancellationToken)
    {
        var run = await _db.IngestRuns
            .Where(r => r.Id == id)
            .Select(r => new IngestRunStatusDto(
                r.Id,
                r.SourceType,
                r.SourceLabel,
                r.Status,
                r.FilesProcessed,
                r.FilesFailed,
                r.ErrorMessage,
                r.StartedAt,
                r.FinishedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("documents/{id:guid}")]
    public async Task<ActionResult<DocumentListItemDto>> GetDocument(
        Guid id,
        CancellationToken cancellationToken)
    {
        var doc = await _db.Documents
            .Where(d => d.Id == id)
            .Select(d => new DocumentListItemDto(
                d.Id,
                d.Title,
                d.SourceType,
                d.DocType,
                d.IngestStatus,
                d.IngestDetail,
                d.CreatedAt,
                null))
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? NotFound() : Ok(doc);
    }
}
