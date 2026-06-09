using Intranet.Api.KnowledgeBase.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Intranet.Api.KnowledgeBase.Data;

public sealed class KnowledgeDbContext : DbContext
{
    public KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<KbDocument> Documents => Set<KbDocument>();
    public DbSet<KbIngestRun> IngestRuns => Set<KbIngestRun>();
    public DbSet<KbProject> Projects => Set<KbProject>();
    public DbSet<KbProjectDocument> ProjectDocuments => Set<KbProjectDocument>();
    public DbSet<KbPrompt> Prompts => Set<KbPrompt>();
    public DbSet<KbChatSession> ChatSessions => Set<KbChatSession>();
    public DbSet<KbChatMessage> ChatMessages => Set<KbChatMessage>();
    public DbSet<KbGeneratedFile> GeneratedFiles => Set<KbGeneratedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<KbDocument>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.IngestRunId).HasColumnName("ingest_run_id");
            e.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(64);
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.SourceUri).HasColumnName("source_uri");
            e.Property(x => x.ExternalId).HasColumnName("external_id");
            e.Property(x => x.MimeType).HasColumnName("mime_type");
            e.Property(x => x.DocType).HasColumnName("doc_type");
            e.Property(x => x.Summary).HasColumnName("summary");
            e.Property(x => x.ModifiedAt).HasColumnName("modified_at");
            e.Property(x => x.StorageUri).HasColumnName("storage_uri");
            e.Property(x => x.IngestStatus).HasColumnName("ingest_status");
            e.Property(x => x.IngestDetail).HasColumnName("ingest_detail");
            e.Property(x => x.UploadedByOid).HasColumnName("uploaded_by_oid");
            e.Property(x => x.IngestJobId).HasColumnName("ingest_job_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<KbProject>(e =>
        {
            e.ToTable("kb_projects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserOid).HasColumnName("user_oid");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Instructions).HasColumnName("instructions");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<KbProjectDocument>(e =>
        {
            e.ToTable("kb_project_documents");
            e.HasKey(x => new { x.ProjectId, x.DocumentId });
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.DocumentId).HasColumnName("document_id");
            e.Property(x => x.AddedAt).HasColumnName("added_at");
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
            e.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId);
        });

        modelBuilder.Entity<KbPrompt>(e =>
        {
            e.ToTable("kb_prompts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserOid).HasColumnName("user_oid");
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<KbIngestRun>(e =>
        {
            e.ToTable("ingest_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SourceType).HasColumnName("source_type");
            e.Property(x => x.SourceLabel).HasColumnName("source_label");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.FinishedAt).HasColumnName("finished_at");
            e.Property(x => x.FilesProcessed).HasColumnName("files_processed");
            e.Property(x => x.FilesFailed).HasColumnName("files_failed");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.ErrorMessage).HasColumnName("error_message");
        });

        modelBuilder.Entity<KbChatSession>(e =>
        {
            e.ToTable("chat_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserOid).HasColumnName("user_oid");
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<KbChatMessage>(e =>
        {
            e.ToTable("chat_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.CitationsJson).HasColumnName("citations").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<KbGeneratedFile>(e =>
        {
            e.ToTable("kb_generated_files");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.MessageId).HasColumnName("message_id");
            e.Property(x => x.UserOid).HasColumnName("user_oid");
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.Filename).HasColumnName("filename");
            e.Property(x => x.MimeType).HasColumnName("mime_type");
            e.Property(x => x.Format).HasColumnName("format");
            e.Property(x => x.StoragePath).HasColumnName("storage_path");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId);
        });
    }
}
