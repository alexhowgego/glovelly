using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGigIntakeIntent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntentEvidenceJson",
                table: "CurrentIntakes",
                type: "character varying(32768)",
                maxLength: 32768,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntentOverride",
                table: "CurrentIntakes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntentEvidenceJson",
                table: "CurrentIntakes");

            migrationBuilder.DropColumn(
                name: "IntentOverride",
                table: "CurrentIntakes");
        }
    }
}
