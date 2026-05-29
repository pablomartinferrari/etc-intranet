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
    private readonly MultifamilyReadingsService _sharePoint;
    private readonly NormalizationService _normalization;
    private readonly ReportGenerationService _reports;
    private readonly ReportExcelExportService _reportExcel;
    private readonly ILogger<EntityController> _logger;

    public EntityController(
        EntityDashboardService dashboard,
        EntityUploadService uploads,
        EntityRowsService rows,
        MultifamilyReadingsService sharePoint,
        NormalizationService normalization,
        ReportGenerationService reports,
        ReportExcelExportService reportExcel,
        ILogger<EntityController> logger)
    {
        _dashboard = dashboard;
        _uploads = uploads;
        _rows = rows;
        _sharePoint = sharePoint;
        _normalization = normalization;
        _reports = reports;
        _reportExcel = reportExcel;
        _logger = logger;
    }

    [HttpGet]
    [HttpGet("summary")]
    public async Task<ActionResult<EntityDashboardDto>> GetDashboard(string jobId, string entitySlug, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _dashboard.GetDashboardAsync(jobId, entitySlug, ct));
    }

    [HttpPost("uploads")]
    public ActionResult UploadDisabled(string jobId, string entitySlug)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            error = "Direct file upload is disabled. Upload files in SharePoint (XRF-SourceFiles), then use import-legacy.",
            code = "direct_upload_disabled",
        });
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

    [HttpGet("source-files")]
    public async Task<ActionResult<IReadOnlyList<SharePointSourceFileDto>>> ListSourceFiles(
        string jobId,
        string entitySlug,
        CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        try
        {
            return Ok(await _sharePoint.GetSourceFilesByJobAsync(jobId, ct));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "SharePoint source file list not available for job {JobId}", jobId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = ex.Message,
                code = "sharepoint_list_unavailable",
            });
        }
    }

    [HttpPost("import-legacy")]
    public async Task<ActionResult<object>> ImportLegacy(string jobId, string entitySlug, [FromBody] ImportLegacyRequest? body, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        try
        {
            var result = await _uploads.ImportLegacyAsync(jobId, entitySlug, body?.Overwrite ?? false, ct);
            return Ok(new { imported = result.Imported, filesAdded = result.FilesAdded, filesSkipped = result.FilesSkipped });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "SharePoint import not available for job {JobId}", jobId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = ex.Message,
                code = "sharepoint_import_unavailable",
            });
        }
    }

    [HttpPost("workspace/clear")]
    public async Task<ActionResult<ClearWorkspaceResult>> ClearWorkspace(string jobId, string entitySlug, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _uploads.ClearWorkspaceAsync(jobId, entitySlug, ct));
    }

    [HttpGet("rows")]
    public async Task<ActionResult<IReadOnlyList<InspectionRowDto>>> GetRows(
        string jobId,
        string entitySlug,
        [FromQuery] string? dataType,
        [FromQuery] string? sourceFile,
        [FromQuery] string? validationStatus,
        [FromQuery] string? search,
        [FromQuery] string? result,
        CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _rows.ListAsync(jobId, entitySlug, dataType, sourceFile, validationStatus, search, result, ct));
    }

    [HttpPatch("rows")]
    public async Task<ActionResult<object>> PatchRows(string jobId, string entitySlug, [FromBody] PatchRowsRequest request, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        var count = await _rows.PatchAsync(jobId, entitySlug, request, ct);
        return Ok(new { updated = count });
    }

    [HttpPost("normalize")]
    public async Task<ActionResult<RunNormalizationResultDto>> RunNormalize(
        string jobId, string entitySlug, [FromBody] RunNormalizationRequest request, CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        return Ok(await _normalization.RunAsync(jobId, entitySlug, request, ct));
    }

    [HttpGet("normalizations")]
    public async Task<ActionResult<IReadOnlyList<NormalizationSuggestionDto>>> ListNormalizations(
        string jobId,
        string entitySlug,
        [FromQuery] string? status,
        [FromQuery] string? fields,
        CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        IReadOnlyList<string>? fieldList = null;
        if (!string.IsNullOrWhiteSpace(fields))
        {
            fieldList = fields
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(f => f.ToLowerInvariant())
                .Where(f => f is "component" or "substrate")
                .Distinct()
                .ToList();
        }

        return Ok(await _normalization.ListAsync(jobId, entitySlug, status, fieldList, ct));
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

    [HttpGet("reports/{reportId:guid}/export")]
    public async Task<IActionResult> ExportReport(
        string jobId,
        string entitySlug,
        Guid reportId,
        CancellationToken ct)
    {
        if (!EntityRegistry.IsValid(entitySlug)) return NotFound();
        var snapshot = await _reports.GetAsync(jobId, entitySlug, reportId, ct);
        if (snapshot == null) return NotFound();

        var bytes = _reportExcel.Export(snapshot.Result, jobId, snapshot.DataType, snapshot.GeneratedAt);
        var fileName = $"LBP-Report-{jobId}-{snapshot.DataType}-{snapshot.GeneratedAt:yyyyMMdd-HHmm}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}

public record PatchNormalizationBody(string Status, string? ApprovedValue);
