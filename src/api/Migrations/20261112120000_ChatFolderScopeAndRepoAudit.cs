using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intranet.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChatFolderScopeAndRepoAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "AgenticChats",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                table: "AgenticRepoFolders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByOid",
                table: "AgenticRepoFolders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "AgenticRepoFolders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "AgenticRepoFolders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByOid",
                table: "AgenticRepoFolders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByOid",
                table: "AgenticRepoDocuments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "AgenticRepoDocuments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByOid",
                table: "AgenticRepoDocuments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FolderPath",
                table: "AgenticRepoDocuments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "AgenticRepoDocuments" AS d
                SET "FolderPath" = f."Path"
                FROM "AgenticRepoFolders" AS f
                WHERE d."FolderId" = f."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AgenticRepoFolders_Path",
                table: "AgenticRepoFolders",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticRepoDocuments_FolderPath",
                table: "AgenticRepoDocuments",
                column: "FolderPath");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticChats_FolderId",
                table: "AgenticChats",
                column: "FolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgenticChats_AgenticRepoFolders_FolderId",
                table: "AgenticChats",
                column: "FolderId",
                principalTable: "AgenticRepoFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgenticChats_AgenticRepoFolders_FolderId",
                table: "AgenticChats");

            migrationBuilder.DropIndex(
                name: "IX_AgenticChats_FolderId",
                table: "AgenticChats");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "AgenticChats");

            migrationBuilder.DropColumn(
                name: "CreatedByName",
                table: "AgenticRepoFolders");

            migrationBuilder.DropColumn(
                name: "CreatedByOid",
                table: "AgenticRepoFolders");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AgenticRepoFolders");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "AgenticRepoFolders");

            migrationBuilder.DropColumn(
                name: "UpdatedByOid",
                table: "AgenticRepoFolders");

            migrationBuilder.DropColumn(
                name: "CreatedByOid",
                table: "AgenticRepoDocuments");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "AgenticRepoDocuments");

            migrationBuilder.DropColumn(
                name: "UpdatedByOid",
                table: "AgenticRepoDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AgenticRepoFolders_Path",
                table: "AgenticRepoFolders");

            migrationBuilder.DropIndex(
                name: "IX_AgenticRepoDocuments_FolderPath",
                table: "AgenticRepoDocuments");

            migrationBuilder.DropColumn(
                name: "FolderPath",
                table: "AgenticRepoDocuments");
        }
    }
}
