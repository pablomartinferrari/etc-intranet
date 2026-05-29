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
            UseNormalizedValues = true,
            GeneratedBy = generatedBy,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
        _db.ReportSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);

        return new ReportSnapshotDto(snapshot.Id, dataType, config.UniformThreshold, true,
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

    private static Dictionary<string, object> BuildReport(List<InspectionRowRecord> rows, ReportConfigRequest config)
    {
        static string Comp(InspectionRowRecord r) => InspectionRowMapper.EffectiveComponent(r);
        static string Sub(InspectionRowRecord r) => InspectionRowMapper.EffectiveSubstrate(r);

        // REQUIREMENTS.md §4.2: classify per unique (normalized) component, not per substrate.
        var groups = rows.GroupBy(r => Comp(r), StringComparer.OrdinalIgnoreCase);
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
            var positives = list.Count(x =>
                x.IsPositive || x.LeadContent >= InspectionRowMapper.LeadPositiveThreshold);
            var negatives = total - positives;
            var pct = total > 0 ? positives * 100.0 / total : 0;
            var substrateLabel = SummarizeSubstrates(list);

            // REQUIREMENTS.md §4.2: >= 40 readings → statistical average (positive if > 2.5%).
            if (total >= StatisticalSampleSize)
            {
                average.Add(new
                {
                    component = g.Key,
                    substrate = substrateLabel,
                    totalReadings = total,
                    positiveCount = positives,
                    negativeCount = negatives,
                    positivePercent = Math.Round(pct, 2),
                    negativePercent = Math.Round(100 - pct, 2),
                    result = pct > PositivePercentThreshold ? "POSITIVE" : "NEGATIVE",
                });
            }
            // < 40 readings, all same result → uniform.
            else if (positives == 0 || positives == total)
            {
                uniform.Add(new
                {
                    component = g.Key,
                    substrate = substrateLabel,
                    result = positives == total ? "POSITIVE" : "NEGATIVE",
                    totalReadings = total,
                });
            }
            // < 40 readings, mixed results → non-uniform (include shot detail for review).
            else
            {
                nonUniform.Add(new
                {
                    component = g.Key,
                    substrate = substrateLabel,
                    positiveCount = positives,
                    negativeCount = negatives,
                    positivePercent = Math.Round(pct, 2),
                    totalReadings = total,
                    readings = list.Select(x => new
                    {
                        x.ReadingId,
                        substrate = Sub(x),
                        x.Location,
                        leadContent = x.LeadContent,
                        isPositive = x.IsPositive || x.LeadContent >= InspectionRowMapper.LeadPositiveThreshold,
                    }),
                });
            }
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["metadata"] = new
            {
                generatedAt = DateTimeOffset.UtcNow,
                statisticalSampleSize = StatisticalSampleSize,
                positivePercentThreshold = PositivePercentThreshold,
                useNormalizedValues = true,
                totalReadings = rows.Count,
                sections = config.Sections,
            },
        };

        if (SectionIncluded(config, "allShots"))
            result["allShots"] = allShots;
        if (SectionIncluded(config, "uniformShots"))
            result["uniformShots"] = uniform;
        if (SectionIncluded(config, "nonUniformShots"))
            result["nonUniformShots"] = nonUniform;
        if (SectionIncluded(config, "averageComponents"))
            result["averageComponents"] = average;

        return result;
    }

    private static bool SectionIncluded(ReportConfigRequest config, string resultKey)
    {
        if (config.Sections is not { Count: > 0 })
            return true;

        string[] aliases = resultKey switch
        {
            "allShots" => ["allShots", "all"],
            "uniformShots" => ["uniformShots", "uniform"],
            "nonUniformShots" => ["nonUniformShots", "nonUniform"],
            "averageComponents" => ["averageComponents", "average"],
            _ => [resultKey],
        };

        return config.Sections.Any(s =>
            aliases.Any(a => string.Equals(a, s, StringComparison.OrdinalIgnoreCase)));
    }

    private static string SummarizeSubstrates(List<InspectionRowRecord> rows)
    {
        static string Sub(InspectionRowRecord r) => InspectionRowMapper.EffectiveSubstrate(r);

        var substrates = rows
            .Select(Sub)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return substrates.Count switch
        {
            0 => "",
            1 => substrates[0],
            _ => "Multiple",
        };
    }
}
