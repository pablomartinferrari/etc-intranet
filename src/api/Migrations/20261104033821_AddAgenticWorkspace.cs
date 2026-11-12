using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intranet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgenticWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgenticProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgenticRepoFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsIndexed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticRepoFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgenticRepoFolders_AgenticRepoFolders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "AgenticRepoFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgenticChats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RealtimeCollaboration = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticChats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgenticChats_AgenticProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "AgenticProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgenticProjectShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrincipalType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PrincipalOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PrincipalDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrincipalEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticProjectShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgenticProjectShares_AgenticProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "AgenticProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgenticRepoDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    IsIndexed = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticRepoDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgenticRepoDocuments_AgenticRepoFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "AgenticRepoFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgenticChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CitationsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgenticChatMessages_AgenticChats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "AgenticChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgenticChatShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrincipalType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PrincipalOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PrincipalDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrincipalEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticChatShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgenticChatShares_AgenticChats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "AgenticChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgenticProjectFiles",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticProjectFiles", x => new { x.ProjectId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_AgenticProjectFiles_AgenticProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "AgenticProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgenticProjectFiles_AgenticRepoDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "AgenticRepoDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgenticRepoChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticRepoChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgenticRepoChunks_AgenticRepoDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "AgenticRepoDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgenticChatMessages_ChatId",
                table: "AgenticChatMessages",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticChats_OwnerOid",
                table: "AgenticChats",
                column: "OwnerOid");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticChats_ProjectId",
                table: "AgenticChats",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticChatShares_ChatId_PrincipalType_PrincipalOid",
                table: "AgenticChatShares",
                columns: new[] { "ChatId", "PrincipalType", "PrincipalOid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgenticProjectFiles_DocumentId",
                table: "AgenticProjectFiles",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticProjects_OwnerOid",
                table: "AgenticProjects",
                column: "OwnerOid");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticProjectShares_ProjectId_PrincipalType_PrincipalOid",
                table: "AgenticProjectShares",
                columns: new[] { "ProjectId", "PrincipalType", "PrincipalOid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgenticRepoChunks_DocumentId",
                table: "AgenticRepoChunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticRepoDocuments_FolderId",
                table: "AgenticRepoDocuments",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticRepoFolders_ParentId",
                table: "AgenticRepoFolders",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgenticChatMessages");

            migrationBuilder.DropTable(
                name: "AgenticChatShares");

            migrationBuilder.DropTable(
                name: "AgenticProjectFiles");

            migrationBuilder.DropTable(
                name: "AgenticProjectShares");

            migrationBuilder.DropTable(
                name: "AgenticRepoChunks");

            migrationBuilder.DropTable(
                name: "AgenticChats");

            migrationBuilder.DropTable(
                name: "AgenticRepoDocuments");

            migrationBuilder.DropTable(
                name: "AgenticProjects");

            migrationBuilder.DropTable(
                name: "AgenticRepoFolders");
        }
    }
}
