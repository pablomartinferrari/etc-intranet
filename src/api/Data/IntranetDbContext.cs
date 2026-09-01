using Intranet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Data;

public class IntranetDbContext(DbContextOptions<IntranetDbContext> options) : DbContext(options)
{
    public DbSet<SiteMessage> SiteMessages => Set<SiteMessage>();

    public DbSet<PursuitCloseout> PursuitCloseouts => Set<PursuitCloseout>();

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
    }
}
