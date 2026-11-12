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

    public async Task<RunNormalizationResultDto> RunAsync(
        string jobIdentifier,
        string entitySlug,
        RunNormalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var fields = NormalizeRequestedFields(request.Fields);
        if (fields.Count == 0)
            throw new InvalidOperationException("At least one field (component or substrate) is required.");
        var rows = await GetScopedRows(entity.Id, request, cancellationToken);

        var staleForRun = await _db.NormalizationSuggestions
            .Where(s => s.JobEntityId == entity.Id && fields.Contains(s.FieldName))
            .ToListAsync(cancellationToken);
        if (staleForRun.Count > 0)
            _db.NormalizationSuggestions.RemoveRange(staleForRun);

        var needsReview = new List<NormalizationSuggestionRecord>();
        var autoAppliedCount = 0;

        foreach (var field in fields)
        {
            var fieldRows = FilterRowsForField(rows, request, field);
            var groups = IsSubstrateField(field)
                ? GroupSubstrates(fieldRows)
                : GroupComponents(fieldRows);

            foreach (var group in groups)
            {
                if (string.IsNullOrWhiteSpace(group.DisplayOriginal)) continue;

                var cacheOriginal = group.Variants[0];
                var cached = await _db.NormalizationCaches
                    .FirstOrDefaultAsync(
                        c => c.FieldName == field && c.OriginalValue == cacheOriginal,
                        cancellationToken);

                var suggested = cached?.NormalizedValue ?? SuggestCanonical(group.Variants[0], field);
                var confidence = cached != null ? "high" : ScoreConfidence(group.DisplayOriginal, suggested);
                var isExactMatch = group.Variants.All(v => ValuesMatch(v, suggested));

                var rec = new NormalizationSuggestionRecord
                {
                    Id = Guid.NewGuid(),
                    JobEntityId = entity.Id,
                    FieldName = field,
                    OriginalValue = group.DisplayOriginal,
                    SuggestedValue = suggested,
                    AffectedRowCount = group.Count,
                    DataType = group.DataType,
                    Confidence = confidence,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };

                if (isExactMatch)
                {
                    ApplyClusterToMatchingRows(fieldRows, field, group.Variants, suggested);
                    rec.Status = "applied";
                    rec.ApprovedAt = DateTimeOffset.UtcNow;
                    foreach (var variant in group.Variants)
                        await UpsertCacheAsync(field, variant, suggested, cancellationToken);
                    autoAppliedCount++;
                    _db.NormalizationSuggestions.Add(rec);
                }
                else
                {
                    rec.Status = "pending";
                    _db.NormalizationSuggestions.Add(rec);
                    needsReview.Add(rec);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new RunNormalizationResultDto(
            needsReview.Select(ToDto).ToList(),
            autoAppliedCount);
    }

    public async Task<IReadOnlyList<NormalizationSuggestionDto>> ListAsync(
        string jobIdentifier,
        string entitySlug,
        string? status,
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var query = _db.NormalizationSuggestions.Where(s => s.JobEntityId == entity.Id);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(s => s.Status == status);
        if (fields is { Count: > 0 })
            query = query.Where(s => fields.Contains(s.FieldName));
        var list = await query.OrderByDescending(s => s.UpdatedAt).ThenBy(s => s.FieldName).ThenBy(s => s.OriginalValue)
            .ToListAsync(cancellationToken);

        // One row per distinct original display (latest wins) — avoids duplicates after re-runs.
        var deduped = new List<NormalizationSuggestionRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in list)
        {
            var key = $"{s.FieldName}|{s.OriginalValue}";
            if (!seen.Add(key)) continue;
            deduped.Add(s);
        }

        return deduped.Select(ToDto).ToList();
    }

    /// <summary>Suggestions that still need a human decision (excludes auto-applied exact matches).</summary>
    public static bool NeedsReview(NormalizationSuggestionDto s)
    {
        if (string.Equals(s.Status, "rejected", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(s.Status, "edited", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Status, "pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Status, "approved", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(s.Status, "applied", StringComparison.OrdinalIgnoreCase))
            return DiffersFromAutoApplied(s);

        return true;
    }

    private static bool DiffersFromAutoApplied(NormalizationSuggestionDto s)
    {
        if (!string.IsNullOrWhiteSpace(s.ApprovedValue))
            return true;

        var effective = (s.ApprovedValue ?? s.SuggestedValue).Trim();
        var suggested = s.SuggestedValue.Trim();
        var originals = ParseOriginalVariants(s.OriginalValue);

        if (!ValuesMatch(effective, suggested))
            return true;

        return !originals.All(o => ValuesMatch(o, effective));
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

        var nextStatus = status;
        if (!string.IsNullOrWhiteSpace(approvedValue)
            && string.Equals(s.Status, "applied", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase)
            && !ValuesMatch(approvedValue, s.SuggestedValue))
        {
            nextStatus = "edited";
        }

        s.Status = nextStatus;
        if (approvedValue != null) s.ApprovedValue = approvedValue;
        s.UpdatedAt = DateTimeOffset.UtcNow;

        if (string.Equals(nextStatus, "approved", StringComparison.OrdinalIgnoreCase))
        {
            var rows = await _db.InspectionRows
                .Where(r => r.JobEntityId == entity.Id)
                .ToListAsync(cancellationToken);
            var value = s.ApprovedValue ?? s.SuggestedValue;
            var variants = ParseOriginalVariants(s.OriginalValue);
            ApplyClusterToMatchingRows(rows, s.FieldName, variants, value);
            foreach (var variant in variants)
                await UpsertCacheAsync(s.FieldName, variant, value, cancellationToken);
            s.Status = "applied";
            s.ApprovedAt = DateTimeOffset.UtcNow;
        }

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
            var variants = ParseOriginalVariants(s.OriginalValue);
            applied += ApplyClusterToMatchingRows(rows, s.FieldName, variants, value);

            s.Status = "applied";
            s.ApprovedAt = DateTimeOffset.UtcNow;
            s.UpdatedAt = DateTimeOffset.UtcNow;

            foreach (var variant in variants)
                await UpsertCacheAsync(s.FieldName, variant, value, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return applied;
    }

    private static int ApplyClusterToMatchingRows(
        IEnumerable<InspectionRowRecord> rows,
        string field,
        IReadOnlyList<string> originalVariants,
        string normalizedValue)
    {
        var variantKeys = originalVariants
            .Select(v => ClusterKey(v, field))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var applied = 0;
        foreach (var row in rows)
        {
            var raw = IsSubstrateField(field) ? row.Substrate : row.Component;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!variantKeys.Contains(ClusterKey(raw, field))) continue;

            if (IsSubstrateField(field))
                row.NormalizedSubstrate = normalizedValue;
            else
                row.NormalizedComponent = normalizedValue;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            applied++;
        }

        return applied;
    }

    private static IReadOnlyList<string> ParseOriginalVariants(string originalValue) =>
        originalValue.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static int ApplyToMatchingRows(
        IEnumerable<InspectionRowRecord> rows,
        string field,
        string originalValue,
        string normalizedValue) =>
        ApplyClusterToMatchingRows(rows, field, ParseOriginalVariants(originalValue), normalizedValue);

    private static bool ValuesMatch(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

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
        return await query.ToListAsync(ct);
    }

    private static IReadOnlyList<string> NormalizeRequestedFields(IReadOnlyList<string> fields)
    {
        if (fields.Count == 0)
            return ["component", "substrate"];

        return fields
            .Select(f => f.Trim().ToLowerInvariant())
            .Where(f => f is "component" or "substrate")
            .Distinct()
            .ToList();
    }

    private static List<InspectionRowRecord> FilterRowsForField(
        List<InspectionRowRecord> rows,
        RunNormalizationRequest request,
        string field)
    {
        if (!string.Equals(request.Scope, "missing", StringComparison.OrdinalIgnoreCase))
            return rows;

        return IsSubstrateField(field)
            ? rows.Where(r => string.IsNullOrWhiteSpace(r.NormalizedSubstrate)).ToList()
            : rows.Where(r => string.IsNullOrWhiteSpace(r.NormalizedComponent)).ToList();
    }

    private static bool IsSubstrateField(string field) =>
        field.Equals("substrate", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ComponentGroup> GroupComponents(List<InspectionRowRecord> rows) =>
        rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Component))
            .GroupBy(r => ClusterKey(r.Component, "component"), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var variants = g.Select(x => x.Component.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var display = variants.Count == 1
                    ? variants[0]
                    : string.Join(" · ", variants);
                return new ComponentGroup(
                    display,
                    variants,
                    g.Count(),
                    g.Select(x => x.DataType).Distinct().Count() > 1 ? "both" : g.First().DataType);
            });

    private static IEnumerable<ComponentGroup> GroupSubstrates(List<InspectionRowRecord> rows) =>
        rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Substrate))
            .GroupBy(r => ClusterKey(r.Substrate!, "substrate"), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var variants = g.Select(x => x.Substrate!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var display = variants.Count == 1
                    ? variants[0]
                    : string.Join(" · ", variants);
                return new ComponentGroup(
                    display,
                    variants,
                    g.Count(),
                    g.Select(x => x.DataType).Distinct().Count() > 1 ? "both" : g.First().DataType);
            });

    private sealed record ComponentGroup(string DisplayOriginal, IReadOnlyList<string> Variants, int Count, string DataType);

    private static string ClusterKey(string? value, string field) =>
        IsSubstrateField(field) ? SubstrateClusterKey(value) : ComponentClusterKey(value);

    /// <summary>Groups minor spelling/plural variants (e.g. Cabinet Casing / Cabinet Casings).</summary>
    private static string ComponentClusterKey(string? component)
    {
        if (string.IsNullOrWhiteSpace(component)) return "";
        var s = Regex.Replace(component.Trim(), @"\s+", " ");
        s = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        if (s.Length > 3
            && s.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && !s.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
            && !s.EndsWith("us", StringComparison.OrdinalIgnoreCase))
        {
            return s[..^1];
        }

        return s;
    }

    private static string SubstrateClusterKey(string? substrate)
    {
        if (string.IsNullOrWhiteSpace(substrate)) return "";
        return Regex.Replace(substrate.Trim(), @"\s+", " ");
    }

    private static string SuggestCanonical(string original, string field) =>
        ClusterKey(original, field);

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
