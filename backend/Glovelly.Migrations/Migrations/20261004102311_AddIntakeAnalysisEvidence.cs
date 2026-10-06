using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeAnalysisEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceJson",
                table: "IntakeAnalysisAttempts",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "IntakeAnalysisAttempts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceJson",
                table: "CurrentIntakes",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceJson",
                table: "IntakeAnalysisAttempts");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "IntakeAnalysisAttempts");

            migrationBuilder.DropColumn(
                name: "EvidenceJson",
                table: "CurrentIntakes");
        }
    }
}
