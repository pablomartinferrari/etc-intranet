using Microsoft.EntityFrameworkCore;
using Intranet.Api.MultifamilyLbp.Data;
using Intranet.Api.MultifamilyLbp.Models;

namespace Intranet.Api.MultifamilyLbp.Services;

public sealed class EntityRowsService
{
    private readonly MultifamilyDbContext _db;
    private readonly JobEntityService _jobEntityService;

    public EntityRowsService(MultifamilyDbContext db, JobEntityService jobEntityService)
    {
        _db = db;
        _jobEntityService = jobEntityService;
    }

    public async Task<IReadOnlyList<InspectionRowDto>> ListAsync(
        string jobIdentifier,
        string entitySlug,
        string? dataType,
        string? sourceFile,
        string? validationStatus,
        string? search,
        string? result,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var query = _db.InspectionRows.Where(r => r.JobEntityId == entity.Id);

        if (!string.IsNullOrEmpty(dataType))
            query = query.Where(r => r.DataType == InspectionRowMapper.NormalizeDataType(dataType));
        if (!string.IsNullOrEmpty(sourceFile))
            query = query.Where(r => r.SourceFileName == sourceFile);
        if (!string.IsNullOrEmpty(validationStatus))
            query = query.Where(r => r.ValidationStatus == validationStatus);
        if (!string.IsNullOrEmpty(result))
        {
            if (result.Equals("positive", StringComparison.OrdinalIgnoreCase))
                query = query.Where(r => r.IsPositive || r.LeadContent >= InspectionRowMapper.LeadPositiveThreshold);
            else if (result.Equals("negative", StringComparison.OrdinalIgnoreCase))
                query = query.Where(r => !r.IsPositive && r.LeadContent < InspectionRowMapper.LeadPositiveThreshold);
        }
        if (!string.IsNullOrEmpty(search))
        {
            var s = search.ToLower();
            query = query.Where(r =>
                r.Component.ToLower().Contains(s) ||
                r.Location.ToLower().Contains(s) ||
                (r.NormalizedComponent != null && r.NormalizedComponent.ToLower().Contains(s)) ||
                r.ReadingId.ToLower().Contains(s));
        }

        var rows = await query.OrderBy(r => r.Location).ThenBy(r => r.ReadingId).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<int> PatchAsync(
        string jobIdentifier,
        string entitySlug,
        PatchRowsRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var ids = request.Rows.Select(r => r.Id).ToList();
        var rows = await _db.InspectionRows
            .Where(r => r.JobEntityId == entity.Id && ids.Contains(r.Id))
            .ToListAsync(cancellationToken);

        foreach (var patch in request.Rows)
        {
            var row = rows.FirstOrDefault(r => r.Id == patch.Id);
            if (row == null) continue;
            if (patch.Location != null) row.Location = patch.Location;
            if (patch.RoomOrArea != null) row.RoomOrArea = patch.RoomOrArea;
            if (patch.NormalizedComponent != null)
                row.NormalizedComponent = patch.NormalizedComponent;
            else if (patch.Component != null)
                row.NormalizedComponent = patch.Component;
            if (patch.NormalizedSubstrate != null)
                row.NormalizedSubstrate = patch.NormalizedSubstrate;
            else if (patch.Substrate != null)
                row.NormalizedSubstrate = patch.Substrate;
            if (patch.ShotCount.HasValue) row.ShotCount = patch.ShotCount.Value;
            if (patch.Notes != null) row.Notes = patch.Notes;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private static InspectionRowDto ToDto(Data.Entities.InspectionRowRecord row) => new(
        row.Id,
        row.ReadingId,
        row.SourceFileName,
        row.DataType,
        row.Location,
        row.RoomOrArea,
        row.Component,
        row.NormalizedComponent,
        row.Substrate,
        row.NormalizedSubstrate,
        row.ShotCount,
        row.Notes,
        row.ValidationStatus,
        row.Color,
        row.LeadContent,
        row.IsPositive,
        row.Side);
}
