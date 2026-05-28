using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intranet.Api.MultifamilyLbp.Data.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobIdentifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ClientName = table.Column<string>(type: "text", nullable: true),
                FacilityName = table.Column<string>(type: "text", nullable: true),
                FacilityAddress = table.Column<string>(type: "text", nullable: true),
                JobStatus = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Jobs", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Jobs_JobIdentifier", table: "Jobs", column: "JobIdentifier", unique: true);

        migrationBuilder.CreateTable(
            name: "NormalizationCaches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FieldName = table.Column<string>(type: "text", nullable: false),
                OriginalValue = table.Column<string>(type: "text", nullable: false),
                NormalizedValue = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_NormalizationCaches", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_NormalizationCaches_FieldName_OriginalValue",
            table: "NormalizationCaches",
            columns: new[] { "FieldName", "OriginalValue" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "JobEntities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobId = table.Column<Guid>(type: "uuid", nullable: false),
                EntitySlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobEntities", x => x.Id);
                table.ForeignKey("FK_JobEntities_Jobs_JobId", x => x.JobId, principalTable: "Jobs", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_JobEntities_JobId_EntitySlug", table: "JobEntities", columns: new[] { "JobId", "EntitySlug" }, unique: true);

        migrationBuilder.CreateTable(
            name: "UploadBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceFileName = table.Column<string>(type: "text", nullable: false),
                SharePointFileUrl = table.Column<string>(type: "text", nullable: true),
                DataType = table.Column<string>(type: "text", nullable: false),
                BuildingProperty = table.Column<string>(type: "text", nullable: true),
                InspectionDate = table.Column<DateOnly>(type: "date", nullable: true),
                BatchName = table.Column<string>(type: "text", nullable: true),
                Status = table.Column<string>(type: "text", nullable: false),
                ValidationWarningsJson = table.Column<string>(type: "text", nullable: true),
                ErrorLogJson = table.Column<string>(type: "text", nullable: true),
                ImportedRowCount = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UploadBatches", x => x.Id);
                table.ForeignKey("FK_UploadBatches_JobEntities_JobEntityId", x => x.JobEntityId, principalTable: "JobEntities", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InspectionRows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                UploadBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                ReadingId = table.Column<string>(type: "text", nullable: false),
                SourceFileName = table.Column<string>(type: "text", nullable: false),
                DataType = table.Column<string>(type: "text", nullable: false),
                Location = table.Column<string>(type: "text", nullable: false),
                RoomOrArea = table.Column<string>(type: "text", nullable: true),
                Component = table.Column<string>(type: "text", nullable: false),
                NormalizedComponent = table.Column<string>(type: "text", nullable: true),
                Substrate = table.Column<string>(type: "text", nullable: true),
                NormalizedSubstrate = table.Column<string>(type: "text", nullable: true),
                ShotCount = table.Column<int>(type: "integer", nullable: false),
                Notes = table.Column<string>(type: "text", nullable: true),
                ValidationStatus = table.Column<string>(type: "text", nullable: false),
                Color = table.Column<string>(type: "text", nullable: false),
                LeadContent = table.Column<double>(type: "double precision", nullable: false),
                IsPositive = table.Column<bool>(type: "boolean", nullable: false),
                UnitNumber = table.Column<string>(type: "text", nullable: true),
                Multifamily = table.Column<string>(type: "text", nullable: true),
                SiteAddress = table.Column<string>(type: "text", nullable: true),
                RoomType = table.Column<string>(type: "text", nullable: true),
                RoomNumber = table.Column<string>(type: "text", nullable: true),
                Side = table.Column<string>(type: "text", nullable: true),
                Condition = table.Column<string>(type: "text", nullable: true),
                Timestamp = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InspectionRows", x => x.Id);
                table.ForeignKey("FK_InspectionRows_JobEntities_JobEntityId", x => x.JobEntityId, principalTable: "JobEntities", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_InspectionRows_UploadBatches_UploadBatchId", x => x.UploadBatchId, principalTable: "UploadBatches", principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(name: "IX_InspectionRows_JobEntityId", table: "InspectionRows", column: "JobEntityId");
        migrationBuilder.CreateIndex(name: "IX_InspectionRows_JobEntityId_ReadingId", table: "InspectionRows", columns: new[] { "JobEntityId", "ReadingId" });

        migrationBuilder.CreateTable(
            name: "NormalizationSuggestions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                FieldName = table.Column<string>(type: "text", nullable: false),
                OriginalValue = table.Column<string>(type: "text", nullable: false),
                SuggestedValue = table.Column<string>(type: "text", nullable: false),
                ApprovedValue = table.Column<string>(type: "text", nullable: true),
                AffectedRowCount = table.Column<int>(type: "integer", nullable: false),
                DataType = table.Column<string>(type: "text", nullable: false),
                Confidence = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                ApprovedBy = table.Column<string>(type: "text", nullable: true),
                ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NormalizationSuggestions", x => x.Id);
                table.ForeignKey("FK_NormalizationSuggestions_JobEntities_JobEntityId", x => x.JobEntityId, principalTable: "JobEntities", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_NormalizationSuggestions_JobEntityId_FieldName_OriginalValue",
            table: "NormalizationSuggestions",
            columns: new[] { "JobEntityId", "FieldName", "OriginalValue" });

        migrationBuilder.CreateTable(
            name: "ReportSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                DataType = table.Column<string>(type: "text", nullable: false),
                ConfigJson = table.Column<string>(type: "text", nullable: false),
                ResultJson = table.Column<string>(type: "text", nullable: false),
                UniformThreshold = table.Column<double>(type: "double precision", nullable: false),
                UseNormalizedValues = table.Column<bool>(type: "boolean", nullable: false),
                GeneratedBy = table.Column<string>(type: "text", nullable: true),
                GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReportSnapshots", x => x.Id);
                table.ForeignKey("FK_ReportSnapshots_JobEntities_JobEntityId", x => x.JobEntityId, principalTable: "JobEntities", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InspectionRows");
        migrationBuilder.DropTable(name: "NormalizationSuggestions");
        migrationBuilder.DropTable(name: "NormalizationCaches");
        migrationBuilder.DropTable(name: "ReportSnapshots");
        migrationBuilder.DropTable(name: "UploadBatches");
        migrationBuilder.DropTable(name: "JobEntities");
        migrationBuilder.DropTable(name: "Jobs");
    }
}
