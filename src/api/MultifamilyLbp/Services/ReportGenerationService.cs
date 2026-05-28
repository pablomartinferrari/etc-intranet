using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Intranet.Api.MultifamilyLbp.Data;
using Intranet.Api.MultifamilyLbp.Data.Entities;
using Intranet.Api.MultifamilyLbp.Models;

namespace Intranet.Api.MultifamilyLbp.Services;

public sealed class ReportGenerationService
{
    private const int StatisticalSampleSize = 40;
    private const double PositivePercentThreshold = 2.5;

    private readonly MultifamilyDbContext _db;
    private readonly JobEntityService _jobEntityService;

    public ReportGenerationService(MultifamilyDbContext db, JobEntityService jobEntityService)
    {
        _db = db;
        _jobEntityService = jobEntityService;
    }

    public async Task<ReportSnapshotDto> GenerateAsync(
        string jobIdentifier,
        string entitySlug,
        ReportConfigRequest config,
        string? generatedBy,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var dataType = InspectionRowMapper.NormalizeDataType(config.DataType);
        var rows = await _db.InspectionRows
            .Where(r => r.JobEntityId == entity.Id && r.DataType == dataType)
            .ToListAsync(cancellationToken);

        var result = BuildReport(rows, config);
        var snapshot = new ReportSnapshotRecord
        {
            Id = Guid.NewGuid(),
            JobEntityId = entity.Id,
            DataType = dataType,
            ConfigJson = JsonSerializer.Serialize(config),
            ResultJson = JsonSerializer.Serialize(result),
            UniformThreshold = config.UniformThreshold,
            UseNormalizedValues = config.UseNormalizedValues,
            GeneratedBy = generatedBy,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
        _db.ReportSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);

        return new ReportSnapshotDto(snapshot.Id, dataType, config.UniformThreshold, config.UseNormalizedValues,
            snapshot.GeneratedAt, generatedBy, result);
    }

    public async Task<ReportSnapshotDto?> GetAsync(
        string jobIdentifier,
        string entitySlug,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var snapshot = await _db.ReportSnapshots
            .FirstOrDefaultAsync(r => r.Id == reportId && r.JobEntityId == entity.Id, cancellationToken);
        if (snapshot == null) return null;
        var result = JsonSerializer.Deserialize<object>(snapshot.ResultJson) ?? new { };
        return new ReportSnapshotDto(snapshot.Id, snapshot.DataType, snapshot.UniformThreshold,
            snapshot.UseNormalizedValues, snapshot.GeneratedAt, snapshot.GeneratedBy, result);
    }

    public async Task<ReportSnapshotDto?> GetLatestAsync(
        string jobIdentifier,
        string entitySlug,
        string? dataType,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var query = _db.ReportSnapshots.Where(r => r.JobEntityId == entity.Id);
        if (!string.IsNullOrEmpty(dataType))
            query = query.Where(r => r.DataType == InspectionRowMapper.NormalizeDataType(dataType));
        var snapshot = await query.OrderByDescending(r => r.GeneratedAt).FirstOrDefaultAsync(cancellationToken);
        if (snapshot == null) return null;
        var result = JsonSerializer.Deserialize<object>(snapshot.ResultJson) ?? new { };
        return new ReportSnapshotDto(snapshot.Id, snapshot.DataType, snapshot.UniformThreshold,
            snapshot.UseNormalizedValues, snapshot.GeneratedAt, snapshot.GeneratedBy, result);
    }

    private static object BuildReport(List<InspectionRowRecord> rows, ReportConfigRequest config)
    {
        string Comp(InspectionRowRecord r) => config.UseNormalizedValues
            ? r.NormalizedComponent ?? r.Component
            : r.Component;
        string Sub(InspectionRowRecord r) => config.UseNormalizedValues
            ? r.NormalizedSubstrate ?? r.Substrate ?? ""
            : r.Substrate ?? "";

        var groups = rows.GroupBy(r => (Comp(r), Sub(r)));
        var allShots = rows.Select(r => new
        {
            r.ReadingId,
            component = Comp(r),
            substrate = Sub(r),
            r.Location,
            r.LeadContent,
            r.IsPositive,
            r.Color,
        }).ToList();

        var average = new List<object>();
        var uniform = new List<object>();
        var nonUniform = new List<object>();

        foreach (var g in groups)
        {
            var list = g.ToList();
            var total = list.Count;
            var positives = list.Count(x => x.IsPositive);
            var pct = total > 0 ? positives * 100.0 / total : 0;

            if (total >= StatisticalSampleSize)
            {
                average.Add(new
                {
                    component = g.Key.Item1,
                    substrate = g.Key.Item2,
                    totalReadings = total,
                    positiveCount = positives,
                    positivePercent = Math.Round(pct, 2),
                    result = pct >= PositivePercentThreshold ? "POSITIVE" : "NEGATIVE",
                });
            }
            else if (total >= config.UniformThreshold)
            {
                uniform.Add(new
                {
                    component = g.Key.Item1,
                    substrate = g.Key.Item2,
                    totalReadings = total,
                    positiveCount = positives,
                    shotCount = total,
                });
            }
            else
            {
                nonUniform.Add(new
                {
                    component = g.Key.Item1,
                    substrate = g.Key.Item2,
                    readings = list.Select(x => new { x.ReadingId, x.LeadContent, x.IsPositive, x.Location }),
                });
            }
        }

        return new
        {
            metadata = new
            {
                generatedAt = DateTimeOffset.UtcNow,
                uniformThreshold = config.UniformThreshold,
                useNormalizedValues = config.UseNormalizedValues,
                totalReadings = rows.Count,
            },
            allShots,
            uniformShots = uniform,
            nonUniformShots = nonUniform,
            byComponent = groups.GroupBy(g => g.Key.Item1).Select(cg => new
            {
                component = cg.Key,
                count = cg.Sum(x => x.Count()),
            }),
            bySubstrate = groups.GroupBy(g => g.Key.Item2).Where(g => !string.IsNullOrEmpty(g.Key)).Select(sg => new
            {
                substrate = sg.Key,
                count = sg.Sum(x => x.Count()),
            }),
            averageComponents = average,
            exceptions = rows.Where(r => r.ValidationStatus != "clean").Select(r => new
            {
                r.ReadingId,
                r.ValidationStatus,
                r.Component,
                r.Location,
            }),
        };
    }
}
