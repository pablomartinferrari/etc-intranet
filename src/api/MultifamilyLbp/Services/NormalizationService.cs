using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Intranet.Api.MultifamilyLbp.Data;
using Intranet.Api.MultifamilyLbp.Data.Entities;
using Intranet.Api.MultifamilyLbp.Models;

namespace Intranet.Api.MultifamilyLbp.Services;

public sealed class NormalizationService
{
    private readonly MultifamilyDbContext _db;
    private readonly JobEntityService _jobEntityService;

    public NormalizationService(MultifamilyDbContext db, JobEntityService jobEntityService)
    {
        _db = db;
        _jobEntityService = jobEntityService;
    }

    public async Task<IReadOnlyList<NormalizationSuggestionDto>> RunAsync(
        string jobIdentifier,
        string entitySlug,
        RunNormalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var rows = await GetScopedRows(entity.Id, request, cancellationToken);

        var fields = request.Fields.Count > 0 ? request.Fields : ["component", "substrate"];
        var suggestions = new List<NormalizationSuggestionRecord>();

        foreach (var field in fields)
        {
            var groups = field.Equals("substrate", StringComparison.OrdinalIgnoreCase)
                ? GroupSubstrates(rows)
                : GroupComponents(rows);

            foreach (var (original, affected, dataType) in groups)
            {
                if (string.IsNullOrWhiteSpace(original)) continue;
                var cached = await _db.NormalizationCaches
                    .FirstOrDefaultAsync(
                        c => c.FieldName == field && c.OriginalValue == original,
                        cancellationToken);

                var suggested = cached?.NormalizedValue ?? SuggestCanonical(original, field);
                var confidence = cached != null ? "high" : ScoreConfidence(original, suggested);

                var existing = await _db.NormalizationSuggestions
                    .FirstOrDefaultAsync(
                        s => s.JobEntityId == entity.Id && s.FieldName == field && s.OriginalValue == original && s.Status == "pending",
                        cancellationToken);

                if (existing != null)
                {
                    existing.SuggestedValue = suggested;
                    existing.AffectedRowCount = affected;
                    existing.Confidence = confidence;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                    suggestions.Add(existing);
                }
                else
                {
                    var rec = new NormalizationSuggestionRecord
                    {
                        Id = Guid.NewGuid(),
                        JobEntityId = entity.Id,
                        FieldName = field,
                        OriginalValue = original,
                        SuggestedValue = suggested,
                        AffectedRowCount = affected,
                        DataType = dataType,
                        Confidence = confidence,
                        Status = "pending",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    };
                    _db.NormalizationSuggestions.Add(rec);
                    suggestions.Add(rec);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return suggestions.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<NormalizationSuggestionDto>> ListAsync(
        string jobIdentifier,
        string entitySlug,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var query = _db.NormalizationSuggestions.Where(s => s.JobEntityId == entity.Id);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(s => s.Status == status);
        var list = await query.OrderBy(s => s.FieldName).ThenBy(s => s.OriginalValue).ToListAsync(cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<NormalizationSuggestionDto?> PatchAsync(
        string jobIdentifier,
        string entitySlug,
        Guid suggestionId,
        string status,
        string? approvedValue,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var s = await _db.NormalizationSuggestions
            .FirstOrDefaultAsync(x => x.Id == suggestionId && x.JobEntityId == entity.Id, cancellationToken);
        if (s == null) return null;
        s.Status = status;
        if (approvedValue != null) s.ApprovedValue = approvedValue;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(s);
    }

    public async Task<int> ApplyApprovedAsync(
        string jobIdentifier,
        string entitySlug,
        ApplyNormalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var suggestions = await _db.NormalizationSuggestions
            .Where(s => s.JobEntityId == entity.Id && request.SuggestionIds.Contains(s.Id))
            .Where(s => s.Status == "approved" || s.Status == "edited")
            .ToListAsync(cancellationToken);

        var rows = await _db.InspectionRows.Where(r => r.JobEntityId == entity.Id).ToListAsync(cancellationToken);
        var applied = 0;

        foreach (var s in suggestions)
        {
            var value = s.ApprovedValue ?? s.SuggestedValue;
            foreach (var row in rows)
            {
                var match = s.FieldName == "substrate"
                    ? string.Equals(row.Substrate, s.OriginalValue, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(row.Component, s.OriginalValue, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;

                if (s.FieldName == "substrate")
                    row.NormalizedSubstrate = value;
                else
                    row.NormalizedComponent = value;
                row.UpdatedAt = DateTimeOffset.UtcNow;
                applied++;
            }

            s.Status = "applied";
            s.ApprovedAt = DateTimeOffset.UtcNow;
            s.UpdatedAt = DateTimeOffset.UtcNow;

            await UpsertCacheAsync(s.FieldName, s.OriginalValue, value, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return applied;
    }

    private async Task UpsertCacheAsync(string field, string original, string normalized, CancellationToken ct)
    {
        var cache = await _db.NormalizationCaches
            .FirstOrDefaultAsync(c => c.FieldName == field && c.OriginalValue == original, ct);
        if (cache == null)
        {
            _db.NormalizationCaches.Add(new NormalizationCacheRecord
            {
                Id = Guid.NewGuid(),
                FieldName = field,
                OriginalValue = original,
                NormalizedValue = normalized,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            cache.NormalizedValue = normalized;
            cache.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task<List<InspectionRowRecord>> GetScopedRows(
        Guid jobEntityId,
        RunNormalizationRequest request,
        CancellationToken ct)
    {
        var query = _db.InspectionRows.Where(r => r.JobEntityId == jobEntityId);
        if (!string.IsNullOrEmpty(request.DataType))
            query = query.Where(r => r.DataType == InspectionRowMapper.NormalizeDataType(request.DataType));
        if (request.RowIds is { Count: > 0 })
            query = query.Where(r => request.RowIds.Contains(r.Id));
        if (request.Scope == "missing")
        {
            query = query.Where(r =>
                string.IsNullOrEmpty(r.NormalizedComponent) || string.IsNullOrEmpty(r.NormalizedSubstrate));
        }
        return await query.ToListAsync(ct);
    }

    private static IEnumerable<(string original, int count, string dataType)> GroupComponents(List<InspectionRowRecord> rows) =>
        rows.GroupBy(r => r.Component.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Count(), g.Select(x => x.DataType).Distinct().Count() > 1 ? "both" : g.First().DataType));

    private static IEnumerable<(string original, int count, string dataType)> GroupSubstrates(List<InspectionRowRecord> rows) =>
        rows.Where(r => !string.IsNullOrWhiteSpace(r.Substrate))
            .GroupBy(r => r.Substrate!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Count(), g.Select(x => x.DataType).Distinct().Count() > 1 ? "both" : g.First().DataType));

    private static string SuggestCanonical(string original, string field)
    {
        var s = Regex.Replace(original.Trim(), @"\s+", " ");
        if (field == "substrate")
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }

    private static string ScoreConfidence(string original, string suggested)
    {
        if (string.Equals(original, suggested, StringComparison.OrdinalIgnoreCase)) return "high";
        if (original.Length <= 3) return "low";
        return "medium";
    }

    private static NormalizationSuggestionDto ToDto(NormalizationSuggestionRecord s) => new(
        s.Id, s.FieldName, s.OriginalValue, s.SuggestedValue, s.ApprovedValue,
        s.AffectedRowCount, s.DataType, s.Confidence, s.Status);
}
