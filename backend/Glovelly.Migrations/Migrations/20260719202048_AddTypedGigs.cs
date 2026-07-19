using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTypedGigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Gigs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Performance");

            migrationBuilder.AddColumn<string>(
                name: "ProposedGigType",
                table: "GigImportDrafts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Performance");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Gigs");

            migrationBuilder.DropColumn(
                name: "ProposedGigType",
                table: "GigImportDrafts");
        }
    }
}
