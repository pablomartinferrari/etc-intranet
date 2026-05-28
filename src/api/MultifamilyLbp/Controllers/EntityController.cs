using Microsoft.AspNetCore.Mvc;
using Intranet.Api.MultifamilyLbp.Config;
using Intranet.Api.MultifamilyLbp.Models;
using Intranet.Api.MultifamilyLbp.Services;

namespace Intranet.Api.MultifamilyLbp.Controllers;

[ApiController]
[Route("api/jobs/{jobId}/{entitySlug}")]
public sealed class EntityController : ControllerBase
{
    private readonly EntityDashboardService _dashboard;
    private readonly EntityUploadService _uploads;
    private readonly EntityRowsService _rows;
    private readonly NormalizationService _normalization;
    private readonly ReportGenerationService _reports;

    public EntityController(
        EntityDashboardService dashboard,
        EntityUploadService uploads,
        EntityRowsService rows,
        NormalizationService normalization,
        ReportGenerationService reports)
    {
        _dashboard = dashboard;
        _uploads = uploads;
        _rows = rows;
        _normalization = normalization;
        _reports = reports;
    }

    [HttpGet]
    [HttpGet("summary")]
    public async Task<ActionResult<EntityDashboardDto>> GetDashboard(string jobId, string entitySlug, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _dashboard.GetDashboardAsync(jobId, entitySlug, ct));
    }

    [HttpPost("uploads")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<UploadResultDto>> Upload(
        string jobId,
        string entitySlug,
        IFormFile file,
        [FromForm] string dataType,
        [FromForm] string? buildingProperty,
        [FromForm] DateOnly? inspectionDate,
        [FromForm] string? batchName,
        CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        if (file == null || file.Length == 0) return BadRequest("File is required.");
        if (string.IsNullOrWhiteSpace(dataType)) return BadRequest("dataType is required (units or commonAreas).");
        var result = await _uploads.UploadAsync(jobId, entitySlug, file, dataType, buildingProperty, inspectionDate, batchName, ct);
        return Ok(result);
    }

    [HttpGet("uploads")]
    public async Task<ActionResult<IReadOnlyList<UploadBatchDto>>> ListUploads(string jobId, string entitySlug, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _uploads.ListBatchesAsync(jobId, entitySlug, ct));
    }

    [HttpGet("uploads/{batchId:guid}/results")]
    public async Task<ActionResult<UploadResultDto>> UploadResults(string jobId, string entitySlug, Guid batchId, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        var result = await _uploads.GetBatchResultsAsync(jobId, entitySlug, batchId, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("uploads/{batchId:guid}")]
    public async Task<IActionResult> DeleteBatch(string jobId, string entitySlug, Guid batchId, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return await _uploads.DeleteBatchAsync(jobId, entitySlug, batchId, ct) ? NoContent() : NotFound();
    }

    [HttpPost("import-legacy")]
    public async Task<ActionResult<object>> ImportLegacy(string jobId, string entitySlug, [FromBody] ImportLegacyRequest? body, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        var count = await _uploads.ImportLegacyAsync(jobId, entitySlug, body?.Overwrite ?? false, ct);
        return Ok(new { imported = count });
    }

    [HttpGet("rows")]
    public async Task<ActionResult<IReadOnlyList<InspectionRowDto>>> GetRows(
        string jobId,
        string entitySlug,
        [FromQuery] string? dataType,
        [FromQuery] string? sourceFile,
        [FromQuery] string? validationStatus,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _rows.ListAsync(jobId, entitySlug, dataType, sourceFile, validationStatus, search, ct));
    }

    [HttpPatch("rows")]
    public async Task<ActionResult<object>> PatchRows(string jobId, string entitySlug, [FromBody] PatchRowsRequest request, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        var count = await _rows.PatchAsync(jobId, entitySlug, request, ct);
        return Ok(new { updated = count });
    }

    [HttpPost("normalize")]
    public async Task<ActionResult<IReadOnlyList<NormalizationSuggestionDto>>> RunNormalize(
        string jobId, string entitySlug, [FromBody] RunNormalizationRequest request, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _normalization.RunAsync(jobId, entitySlug, request, ct));
    }

    [HttpGet("normalizations")]
    public async Task<ActionResult<IReadOnlyList<NormalizationSuggestionDto>>> ListNormalizations(
        string jobId, string entitySlug, [FromQuery] string? status, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _normalization.ListAsync(jobId, entitySlug, status, ct));
    }

    [HttpPatch("normalizations/{suggestionId:guid}")]
    public async Task<ActionResult<NormalizationSuggestionDto>> PatchNormalization(
        string jobId, string entitySlug, Guid suggestionId,
        [FromBody] PatchNormalizationBody body, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        var result = await _normalization.PatchAsync(jobId, entitySlug, suggestionId, body.Status, body.ApprovedValue, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("normalizations/apply")]
    public async Task<ActionResult<object>> ApplyNormalizations(
        string jobId, string entitySlug, [FromBody] ApplyNormalizationRequest request, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        var count = await _normalization.ApplyApprovedAsync(jobId, entitySlug, request, ct);
        return Ok(new { rowsUpdated = count });
    }

    [HttpPost("reports")]
    public async Task<ActionResult<ReportSnapshotDto>> GenerateReport(
        string jobId, string entitySlug, [FromBody] ReportConfigRequest config, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _reports.GenerateAsync(jobId, entitySlug, config, User.Identity?.Name, ct));
    }

    [HttpGet("reports/latest")]
    public async Task<ActionResult<ReportSnapshotDto>> LatestReport(
        string jobId, string entitySlug, [FromQuery] string? dataType, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        var r = await _reports.GetLatestAsync(jobId, entitySlug, dataType, ct);
        return r == null ? NotFound() : Ok(r);
    }

    [HttpGet("reports/{reportId:guid}")]
    public async Task<ActionResult<ReportSnapshotDto>> GetReport(
        string jobId, string entitySlug, Guid reportId, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        var r = await _reports.GetAsync(jobId, entitySlug, reportId, ct);
        return r == null ? NotFound() : Ok(r);
    }
}

public record PatchNormalizationBody(string Status, string? ApprovedValue);
