using Intranet.Api.Data.Entities;
using Intranet.Api.FeatureRequests;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Data;

public class IntranetDbContext(DbContextOptions<IntranetDbContext> options) : DbContext(options)
{
    public DbSet<SiteMessage> SiteMessages => Set<SiteMessage>();

    public DbSet<PursuitCloseout> PursuitCloseouts => Set<PursuitCloseout>();

    public DbSet<FeatureRequest> FeatureRequests => Set<FeatureRequest>();

    public DbSet<AgentSource> AgentSources => Set<AgentSource>();

    public DbSet<AgentSourceJob> AgentSourceJobs => Set<AgentSourceJob>();

    public DbSet<AgentSourceDocument> AgentSourceDocuments => Set<AgentSourceDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SiteMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Body).HasMaxLength(2000);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<PursuitCloseout>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PursuitId).HasMaxLength(200);
            entity.Property(e => e.OpportunityId).HasMaxLength(200);
            entity.Property(e => e.Outcome).HasMaxLength(32);
            entity.Property(e => e.ReasonCode).HasMaxLength(64);
            entity.Property(e => e.Note).HasMaxLength(2000);
            entity.HasIndex(e => e.PursuitId).IsUnique();
            entity.HasIndex(e => e.UpdatedAt);
        });

        modelBuilder.Entity<FeatureRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Page).HasMaxLength(32);
            entity.Property(e => e.AreaLabel).HasMaxLength(FeatureRequestPages.AreaLabelMaxLength);
            entity.Property(e => e.CreatedBy).HasMaxLength(320);
            entity.Property(e => e.RawText).HasMaxLength(8000);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Problem).HasMaxLength(4000);
            entity.Property(e => e.DesiredBehavior).HasMaxLength(4000);
            entity.Property(e => e.DataInvolved).HasMaxLength(4000);
            entity.Property(e => e.AcceptanceCriteria).HasMaxLength(4000);
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.StructuredBy).HasMaxLength(32);
            entity.Property(e => e.ReviewedBy).HasMaxLength(320);
            entity.Property(e => e.ClosedBy).HasMaxLength(320);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<AgentSource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedByOid).HasMaxLength(64);
            entity.Property(e => e.CreatedBy).HasMaxLength(320);
            entity.Property(e => e.Label).HasMaxLength(200);
            entity.Property(e => e.SiteUrl).HasMaxLength(2000);
            entity.Property(e => e.FolderPath).HasMaxLength(1000);
            entity.Property(e => e.FolderIdentity).HasMaxLength(1200);
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.FolderIdentity);
            entity.HasIndex(e => e.Status);
            entity.HasMany(e => e.Jobs)
                .WithOne(e => e.Source)
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Documents)
                .WithOne(e => e.Source)
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentSourceJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.LimitTier).HasMaxLength(16);
            entity.Property(e => e.ErrorMessage).HasMaxLength(4000);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<AgentSourceDocument>(entity =>
        {
            entity.HasKey(e => new { e.SourceId, e.DocumentId });
        });
    }
}
