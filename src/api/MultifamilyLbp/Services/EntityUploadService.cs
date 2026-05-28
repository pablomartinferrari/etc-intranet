using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Intranet.Api.MultifamilyLbp.Data;
using Intranet.Api.MultifamilyLbp.Data.Entities;
using Intranet.Api.MultifamilyLbp.Models;

namespace Intranet.Api.MultifamilyLbp.Services;

public sealed class EntityUploadService
{
    private readonly MultifamilyDbContext _db;
    private readonly JobEntityService _jobEntityService;
    private readonly IParserClient _parser;
    private readonly MultifamilyReadingsService _legacyReadings;
    private readonly ILogger<EntityUploadService> _logger;

    public EntityUploadService(
        MultifamilyDbContext db,
        JobEntityService jobEntityService,
        IParserClient parser,
        MultifamilyReadingsService legacyReadings,
        ILogger<EntityUploadService> logger)
    {
        _db = db;
        _jobEntityService = jobEntityService;
        _parser = parser;
        _legacyReadings = legacyReadings;
        _logger = logger;
    }

    public async Task<UploadResultDto> UploadAsync(
        string jobIdentifier,
        string entitySlug,
        IFormFile file,
        string dataType,
        string? buildingProperty,
        DateOnly? inspectionDate,
        string? batchName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var normalizedType = InspectionRowMapper.NormalizeDataType(dataType);

        var batch = new UploadBatchRecord
        {
            Id = Guid.NewGuid(),
            JobEntityId = entity.Id,
            SourceFileName = file.FileName,
            DataType = normalizedType,
            BuildingProperty = buildingProperty,
            InspectionDate = inspectionDate,
            BatchName = batchName,
            Status = "validating",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.UploadBatches.Add(batch);

        var warnings = new List<string>();
        var errors = new List<string>();

        try
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();

            var readings = await _parser.ParseFileAsync(bytes, file.FileName, cancellationToken);
            if (readings.Count == 0)
                errors.Add("No valid rows found in file.");

            foreach (var dto in readings)
            {
                var row = InspectionRowMapper.FromDto(dto, entity.Id, batch.Id, file.FileName, normalizedType);
                if (row.ValidationStatus == "warning")
                    warnings.Add($"Row {row.ReadingId}: missing location");
                if (row.ValidationStatus == "error")
                    errors.Add($"Row {row.ReadingId}: missing component");
                _db.InspectionRows.Add(row);
            }

            batch.ImportedRowCount = readings.Count;
            batch.Status = errors.Count > 0 ? "failed" : warnings.Count > 0 ? "imported_with_warnings" : "imported";
            batch.ValidationWarningsJson = warnings.Count > 0 ? JsonSerializer.Serialize(warnings) : null;
            batch.ErrorLogJson = errors.Count > 0 ? JsonSerializer.Serialize(errors) : null;
            batch.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return new UploadResultDto(batch.Id, file.FileName, normalizedType, batch.Status, readings.Count, warnings, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for job {Job} entity {Entity}", jobIdentifier, entitySlug);
            batch.Status = "failed";
            batch.ErrorLogJson = JsonSerializer.Serialize(new[] { ex.Message });
            batch.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<UploadBatchDto>> ListBatchesAsync(
        string jobIdentifier,
        string entitySlug,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var batches = await _db.UploadBatches
            .Where(b => b.JobEntityId == entity.Id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return batches.Select(b =>
        {
            IReadOnlyList<string>? warnings = null;
            if (!string.IsNullOrEmpty(b.ValidationWarningsJson))
            {
                try { warnings = JsonSerializer.Deserialize<List<string>>(b.ValidationWarningsJson); }
                catch { /* ignore */ }
            }
            return new UploadBatchDto(
                b.Id, b.SourceFileName, b.DataType, b.Status, b.ImportedRowCount,
                b.BuildingProperty, b.BatchName, b.CreatedAt, warnings);
        }).ToList();
    }

    public async Task<UploadResultDto?> GetBatchResultsAsync(
        string jobIdentifier,
        string entitySlug,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var batch = await _db.UploadBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.JobEntityId == entity.Id, cancellationToken);
        if (batch == null) return null;

        var warnings = DeserializeList(batch.ValidationWarningsJson);
        var errors = DeserializeList(batch.ErrorLogJson);
        return new UploadResultDto(batch.Id, batch.SourceFileName, batch.DataType, batch.Status, batch.ImportedRowCount, warnings, errors);
    }

    public async Task<bool> DeleteBatchAsync(
        string jobIdentifier,
        string entitySlug,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);
        var batch = await _db.UploadBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.JobEntityId == entity.Id, cancellationToken);
        if (batch == null) return false;

        var rows = _db.InspectionRows.Where(r => r.UploadBatchId == batchId);
        _db.InspectionRows.RemoveRange(rows);
        _db.UploadBatches.Remove(batch);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> ImportLegacyAsync(
        string jobIdentifier,
        string entitySlug,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        var entity = await _jobEntityService.ResolveAsync(jobIdentifier, entitySlug, cancellationToken);

        if (overwrite)
        {
            var existing = _db.InspectionRows.Where(r => r.JobEntityId == entity.Id);
            _db.InspectionRows.RemoveRange(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (await _db.InspectionRows.AnyAsync(r => r.JobEntityId == entity.Id, cancellationToken))
        {
            return 0;
        }

        var units = await _legacyReadings.GetReadingsAsync(jobIdentifier, "Units", cancellationToken);
        var common = await _legacyReadings.GetReadingsAsync(jobIdentifier, "Common Areas", cancellationToken);
        var count = 0;

        foreach (var dto in units)
        {
            _db.InspectionRows.Add(InspectionRowMapper.FromDto(dto, entity.Id, null, "legacy-import", "units"));
            count++;
        }
        foreach (var dto in common)
        {
            _db.InspectionRows.Add(InspectionRowMapper.FromDto(dto, entity.Id, null, "legacy-import", "commonAreas"));
            count++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
