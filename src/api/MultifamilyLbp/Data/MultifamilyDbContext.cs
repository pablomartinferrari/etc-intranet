using Intranet.Api.MultifamilyLbp.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.MultifamilyLbp.Data;

public sealed class MultifamilyDbContext : DbContext
{
    public MultifamilyDbContext(DbContextOptions<MultifamilyDbContext> options) : base(options) { }

    public DbSet<JobRecord> Jobs => Set<JobRecord>();
    public DbSet<JobEntityRecord> JobEntities => Set<JobEntityRecord>();
    public DbSet<UploadBatchRecord> UploadBatches => Set<UploadBatchRecord>();
    public DbSet<InspectionRowRecord> InspectionRows => Set<InspectionRowRecord>();
    public DbSet<NormalizationSuggestionRecord> NormalizationSuggestions => Set<NormalizationSuggestionRecord>();
    public DbSet<ReportSnapshotRecord> ReportSnapshots => Set<ReportSnapshotRecord>();
    public DbSet<NormalizationCacheRecord> NormalizationCaches => Set<NormalizationCacheRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.JobIdentifier).IsUnique();
            e.Property(x => x.JobIdentifier).HasMaxLength(64);
        });

        modelBuilder.Entity<JobEntityRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.JobId, x.EntitySlug }).IsUnique();
            e.Property(x => x.EntitySlug).HasMaxLength(64);
            e.HasOne(x => x.Job).WithMany(x => x.Entities).HasForeignKey(x => x.JobId);
        });

        modelBuilder.Entity<UploadBatchRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.JobEntity).WithMany(x => x.UploadBatches).HasForeignKey(x => x.JobEntityId);
        });

        modelBuilder.Entity<InspectionRowRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.JobEntityId);
            e.HasIndex(x => new { x.JobEntityId, x.ReadingId });
            e.HasOne(x => x.JobEntity).WithMany(x => x.InspectionRows).HasForeignKey(x => x.JobEntityId);
            e.HasOne(x => x.UploadBatch).WithMany(x => x.InspectionRows).HasForeignKey(x => x.UploadBatchId);
        });

        modelBuilder.Entity<NormalizationSuggestionRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.JobEntityId, x.FieldName, x.OriginalValue });
            e.HasOne(x => x.JobEntity).WithMany(x => x.NormalizationSuggestions).HasForeignKey(x => x.JobEntityId);
        });

        modelBuilder.Entity<ReportSnapshotRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.JobEntity).WithMany(x => x.ReportSnapshots).HasForeignKey(x => x.JobEntityId);
        });

        modelBuilder.Entity<NormalizationCacheRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FieldName, x.OriginalValue }).IsUnique();
        });
    }
}
