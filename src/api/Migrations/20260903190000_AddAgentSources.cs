using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intranet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SiteUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FolderPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FolderIdentity = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LatestJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalRequestId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentSourceJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LimitTier = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConfirmedMedium = table.Column<bool>(type: "boolean", nullable: false),
                    ProbeFileCount = table.Column<int>(type: "integer", nullable: false),
                    ProbeTotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    ProbeMaxDepth = table.Column<int>(type: "integer", nullable: false),
                    ProbeAllowedFiles = table.Column<int>(type: "integer", nullable: false),
                    ProbeAllowedBytes = table.Column<long>(type: "bigint", nullable: false),
                    ProbeSkippedFiles = table.Column<int>(type: "integer", nullable: false),
                    ProbeSampleExtensionsJson = table.Column<string>(type: "text", nullable: true),
                    ProbeTruncated = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FilesProcessed = table.Column<int>(type: "integer", nullable: false),
                    FilesFailed = table.Column<int>(type: "integer", nullable: false),
                    FilesSkipped = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSourceJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSourceJobs_AgentSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "AgentSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentSourceDocuments",
                columns: table => new
                {
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSourceDocuments", x => new { x.SourceId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_AgentSourceDocuments_AgentSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "AgentSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSources_CreatedAt",
                table: "AgentSources",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSources_FolderIdentity",
                table: "AgentSources",
                column: "FolderIdentity");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSources_Status",
                table: "AgentSources",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSourceJobs_CreatedAt",
                table: "AgentSourceJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSourceJobs_SourceId",
                table: "AgentSourceJobs",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSourceJobs_Status",
                table: "AgentSourceJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AgentSourceDocuments");
            migrationBuilder.DropTable(name: "AgentSourceJobs");
            migrationBuilder.DropTable(name: "AgentSources");
        }
    }
}
