using Microsoft.EntityFrameworkCore;
using Intranet.Api.MultifamilyLbp.Config;
using Intranet.Api.MultifamilyLbp.Data;
using Intranet.Api.MultifamilyLbp.Models;

namespace Intranet.Api.MultifamilyLbp.Services;

public sealed class EntityDashboardService
{
    private readonly MultifamilyDbContext _db;
    private readonly JobEntityService _jobEntityService;

    public EntityDashboardService(MultifamilyDbContext db, JobEntityService jobEntityService)
    {
        _db = db;
        _jobEntityService = jobEntityService;
    }

    public async Task<EntityDashboardDto> GetDashboardAsync(
        string jobIdentifier,
        string entitySlug,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var def = EntityRegistry.Get(entitySlug)!;

        var rows = await _db.InspectionRows
            .Where(r => r.JobEntityId == entity.Id)
            .ToListAsync(cancellationToken);

        var batches = await _db.UploadBatches
            .CountAsync(b => b.JobEntityId == entity.Id && b.Status.StartsWith("import"), cancellationToken);

        var pendingNorm = await _db.NormalizationSuggestions
            .CountAsync(s => s.JobEntityId == entity.Id && s.Status == "pending", cancellationToken);

        var lastReport = await _db.ReportSnapshots
            .Where(r => r.JobEntityId == entity.Id)
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => (DateTimeOffset?)r.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var units = rows.Count(r => r.DataType == "units");
        var common = rows.Count(r => r.DataType == "commonAreas");

        return new EntityDashboardDto(
            jobIdentifier,
            entitySlug,
            def.DisplayName,
            batches,
            units,
            common,
            rows.Count(r => r.ValidationStatus == "warning"),
            rows.Count(r => r.ValidationStatus == "error"),
            pendingNorm,
            pendingNorm > 0 ? "pending_review" : rows.Any(r => string.IsNullOrEmpty(r.NormalizedComponent)) ? "incomplete" : "complete",
            lastReport,
            rows.Count > 0);
    }
}
