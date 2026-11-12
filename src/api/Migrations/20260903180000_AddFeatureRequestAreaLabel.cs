using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intranet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureRequestAreaLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AreaLabel",
                table: "FeatureRequests",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AreaLabel",
                table: "FeatureRequests");
        }
    }
}
