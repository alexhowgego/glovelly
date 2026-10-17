using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddUnifiedIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutomaticReceiptMatching",
                table: "Users",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "HighConfidence");

            migrationBuilder.AddColumn<bool>(
                name: "GigLinesNeedRefresh",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                table: "GigExternalResources",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "GigExpenses",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                table: "GigExpenses",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CurrentIntakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Text = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    AnalysisState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Intent = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EvidenceJson = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrentIntakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurrentIntakes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeAnalysisAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentIntakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Intent = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EvidenceJson = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAnalysisAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeAnalysisAttempts_CurrentIntakes_CurrentIntakeId",
                        column: x => x.CurrentIntakeId,
                        principalTable: "CurrentIntakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_GigExpenses_Category",
                table: "GigExpenses",
                sql: "\"Category\" IS NULL OR \"Category\" IN ('Travel', 'Meals', 'Accommodation', 'Equipment', 'Other')");

            migrationBuilder.CreateIndex(
                name: "IX_CurrentIntakes_UserId",
                table: "CurrentIntakes",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAnalysisAttempts_CurrentIntakeId",
                table: "IntakeAnalysisAttempts",
                column: "CurrentIntakeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeAnalysisAttempts");

            migrationBuilder.DropTable(
                name: "CurrentIntakes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GigExpenses_Category",
                table: "GigExpenses");

            migrationBuilder.DropColumn(
                name: "AutomaticReceiptMatching",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GigLinesNeedRefresh",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                table: "GigExternalResources");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "GigExpenses");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                table: "GigExpenses");
        }
    }
}
