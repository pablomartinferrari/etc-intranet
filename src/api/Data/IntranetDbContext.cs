using Intranet.Api.Agentic.Data;
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

    public DbSet<AgenticProject> AgenticProjects => Set<AgenticProject>();

    public DbSet<AgenticProjectFile> AgenticProjectFiles => Set<AgenticProjectFile>();

    public DbSet<AgenticProjectShare> AgenticProjectShares => Set<AgenticProjectShare>();

    public DbSet<AgenticChat> AgenticChats => Set<AgenticChat>();

    public DbSet<AgenticChatMessage> AgenticChatMessages => Set<AgenticChatMessage>();

    public DbSet<AgenticChatShare> AgenticChatShares => Set<AgenticChatShare>();

    public DbSet<AgenticRepoFolder> AgenticRepoFolders => Set<AgenticRepoFolder>();

    public DbSet<AgenticRepoDocument> AgenticRepoDocuments => Set<AgenticRepoDocument>();

    public DbSet<AgenticRepoChunk> AgenticRepoChunks => Set<AgenticRepoChunk>();

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

        modelBuilder.Entity<AgenticProject>(entity =>
        {
            entity.ToTable("AgenticProjects");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OwnerOid).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Instructions).HasMaxLength(8000);
            entity.HasIndex(e => e.OwnerOid);
        });

        modelBuilder.Entity<AgenticProjectFile>(entity =>
        {
            entity.ToTable("AgenticProjectFiles");
            entity.HasKey(e => new { e.ProjectId, e.DocumentId });
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Files)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Document)
                .WithMany()
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgenticProjectShare>(entity =>
        {
            entity.ToTable("AgenticProjectShares");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PrincipalType).HasMaxLength(16);
            entity.Property(e => e.PrincipalOid).HasMaxLength(64);
            entity.Property(e => e.PrincipalDisplayName).HasMaxLength(200);
            entity.Property(e => e.PrincipalEmail).HasMaxLength(320);
            entity.Property(e => e.Role).HasMaxLength(16);
            entity.Property(e => e.CreatedByOid).HasMaxLength(64);
            entity.HasIndex(e => new { e.ProjectId, e.PrincipalType, e.PrincipalOid }).IsUnique();
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Shares)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgenticChat>(entity =>
        {
            entity.ToTable("AgenticChats");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OwnerOid).HasMaxLength(64);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.HasIndex(e => e.OwnerOid);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.FolderId);
            entity.HasIndex(e => e.DocumentId);
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Chats)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Folder)
                .WithMany(f => f.Chats)
                .HasForeignKey(e => e.FolderId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Document)
                .WithMany(d => d.AttachedChats)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AgenticChatMessage>(entity =>
        {
            entity.ToTable("AgenticChatMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).HasMaxLength(16);
            entity.Property(e => e.AuthorOid).HasMaxLength(64);
            entity.Property(e => e.AuthorName).HasMaxLength(200);
            entity.HasIndex(e => e.ChatId);
            entity.HasOne(e => e.Chat)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgenticChatShare>(entity =>
        {
            entity.ToTable("AgenticChatShares");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PrincipalType).HasMaxLength(16);
            entity.Property(e => e.PrincipalOid).HasMaxLength(64);
            entity.Property(e => e.PrincipalDisplayName).HasMaxLength(200);
            entity.Property(e => e.PrincipalEmail).HasMaxLength(320);
            entity.Property(e => e.Role).HasMaxLength(16);
            entity.Property(e => e.CreatedByOid).HasMaxLength(64);
            entity.HasIndex(e => new { e.ChatId, e.PrincipalType, e.PrincipalOid }).IsUnique();
            entity.HasOne(e => e.Chat)
                .WithMany(c => c.Shares)
                .HasForeignKey(e => e.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgenticRepoFolder>(entity =>
        {
            entity.ToTable("AgenticRepoFolders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Path).HasMaxLength(1000);
            entity.Property(e => e.CreatedByOid).HasMaxLength(64);
            entity.Property(e => e.CreatedByName).HasMaxLength(200);
            entity.Property(e => e.UpdatedByOid).HasMaxLength(64);
            entity.Property(e => e.UpdatedByName).HasMaxLength(200);
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => e.Path);
            entity.HasOne(e => e.Parent)
                .WithMany(f => f.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgenticRepoDocument>(entity =>
        {
            entity.ToTable("AgenticRepoDocuments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(260);
            entity.Property(e => e.FileType).HasMaxLength(32);
            entity.Property(e => e.MimeType).HasMaxLength(200);
            entity.Property(e => e.OwnerName).HasMaxLength(200);
            entity.Property(e => e.CreatedByOid).HasMaxLength(64);
            entity.Property(e => e.UpdatedByOid).HasMaxLength(64);
            entity.Property(e => e.UpdatedByName).HasMaxLength(200);
            entity.Property(e => e.StoragePath).HasMaxLength(500);
            entity.Property(e => e.FolderPath).HasMaxLength(1000);
            entity.Property(e => e.ExtractionStatus).HasMaxLength(32);
            entity.Property(e => e.ExtractionNote).HasMaxLength(500);
            entity.HasIndex(e => e.FolderId);
            entity.HasIndex(e => e.FolderPath);
            entity.HasIndex(e => e.ExtractionStatus);
            entity.HasOne(e => e.Folder)
                .WithMany(f => f.Documents)
                .HasForeignKey(e => e.FolderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgenticRepoChunk>(entity =>
        {
            entity.ToTable("AgenticRepoChunks");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DocumentId);
            entity.HasOne(e => e.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
