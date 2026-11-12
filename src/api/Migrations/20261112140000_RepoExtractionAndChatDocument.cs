using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intranet.Api.Migrations
{
    /// <inheritdoc />
    public partial class RepoExtractionAndChatDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "AgenticChats",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractionStatus",
                table: "AgenticRepoDocuments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExtractionNote",
                table: "AgenticRepoDocuments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgenticChats_DocumentId",
                table: "AgenticChats",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgenticRepoDocuments_ExtractionStatus",
                table: "AgenticRepoDocuments",
                column: "ExtractionStatus");

            migrationBuilder.AddForeignKey(
                name: "FK_AgenticChats_AgenticRepoDocuments_DocumentId",
                table: "AgenticChats",
                column: "DocumentId",
                principalTable: "AgenticRepoDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgenticChats_AgenticRepoDocuments_DocumentId",
                table: "AgenticChats");

            migrationBuilder.DropIndex(
                name: "IX_AgenticChats_DocumentId",
                table: "AgenticChats");

            migrationBuilder.DropIndex(
                name: "IX_AgenticRepoDocuments_ExtractionStatus",
                table: "AgenticRepoDocuments");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "AgenticChats");

            migrationBuilder.DropColumn(
                name: "ExtractionStatus",
                table: "AgenticRepoDocuments");

            migrationBuilder.DropColumn(
                name: "ExtractionNote",
                table: "AgenticRepoDocuments");
        }
    }
}
