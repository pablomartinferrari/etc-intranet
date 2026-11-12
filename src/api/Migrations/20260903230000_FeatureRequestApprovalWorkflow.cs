using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intranet.Api.Migrations
{
    /// <inheritdoc />
    public partial class FeatureRequestApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "FeatureRequests",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "FeatureRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedBy",
                table: "FeatureRequests",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "FeatureRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "FeatureRequests" SET "Status" = 'approved' WHERE "Status" = 'planned';
                UPDATE "FeatureRequests" SET "Status" = 'shipped' WHERE "Status" = 'done';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "FeatureRequests" SET "Status" = 'planned' WHERE "Status" = 'approved';
                UPDATE "FeatureRequests" SET "Status" = 'done' WHERE "Status" = 'shipped';
                """);

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "FeatureRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "FeatureRequests");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "FeatureRequests");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "FeatureRequests");
        }
    }
}
