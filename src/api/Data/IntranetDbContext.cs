using Intranet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Data;

public class IntranetDbContext(DbContextOptions<IntranetDbContext> options) : DbContext(options)
{
    public DbSet<SiteMessage> SiteMessages => Set<SiteMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SiteMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Body).HasMaxLength(2000);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
