using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Intranet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Page = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Problem = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DesiredBehavior = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DataInvolved = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StructuredBy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureRequests_CreatedAt",
                table: "FeatureRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureRequests_Status",
                table: "FeatureRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureRequests");
        }
    }
}
