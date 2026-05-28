using Microsoft.EntityFrameworkCore;
using Intranet.Api.MultifamilyLbp.Config;
using Intranet.Api.MultifamilyLbp.Data;
using Intranet.Api.MultifamilyLbp.Data.Entities;

namespace Intranet.Api.MultifamilyLbp.Services;

public sealed class JobEntityService
{
    private readonly MultifamilyDbContext _db;

    public JobEntityService(MultifamilyDbContext db) => _db = db;

    public async Task<JobEntityRecord> ResolveAsync(
        string jobIdentifier,
        string entitySlug,
        CancellationToken cancellationToken = default)
    {
        if (!EntityRegistry.IsValid(entitySlug))
            throw new ArgumentException($"Unknown entity slug: {entitySlug}");

        var job = await _db.Jobs
            .FirstOrDefaultAsync(j => j.JobIdentifier == jobIdentifier, cancellationToken);

        if (job == null)
        {
            job = new JobRecord
            {
                Id = Guid.NewGuid(),
                JobIdentifier = jobIdentifier.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _db.Jobs.Add(job);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var entity = await _db.JobEntities
            .Include(e => e.Job)
            .FirstOrDefaultAsync(
                e => e.JobId == job.Id && e.EntitySlug == entitySlug,
                cancellationToken);

        if (entity != null)
            return entity;

        entity = new JobEntityRecord
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            EntitySlug = entitySlug,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.JobEntities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        entity.Job = job;
        return entity;
    }

    public async Task<JobRecord?> GetJobAsync(string jobIdentifier, CancellationToken cancellationToken = default) =>
        await _db.Jobs.FirstOrDefaultAsync(j => j.JobIdentifier == jobIdentifier, cancellationToken);

    public async Task UpdateJobMetadataAsync(
        string jobIdentifier,
        string? clientName,
        string? facilityName,
        string? facilityAddress,
        string? jobStatus,
        CancellationToken cancellationToken = default)
    {
        var job = await GetJobAsync(jobIdentifier, cancellationToken);
        if (job == null) return;
        job.ClientName = clientName;
        job.FacilityName = facilityName;
        job.FacilityAddress = facilityAddress;
        job.JobStatus = jobStatus;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
