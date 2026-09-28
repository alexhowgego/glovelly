using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSetListInterpretationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceEvidenceJson",
                table: "GigSetListItems",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SetListInterpretationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GigId = table.Column<Guid>(type: "uuid", nullable: false),
                    GigExternalResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpreadsheetId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    WorksheetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WorksheetName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceGridJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    SafeErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceGridExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetListInterpretationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetListInterpretationJobs_Gigs_GigId",
                        column: x => x.GigId,
                        principalTable: "Gigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SetListInterpretationJobs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SetListInterpretationJobs_GigId",
                table: "SetListInterpretationJobs",
                column: "GigId");

            migrationBuilder.CreateIndex(
                name: "IX_SetListInterpretationJobs_Status_SourceGridExpiresAtUtc",
                table: "SetListInterpretationJobs",
                columns: new[] { "Status", "SourceGridExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SetListInterpretationJobs_UserId_GigId_CreatedAtUtc",
                table: "SetListInterpretationJobs",
                columns: new[] { "UserId", "GigId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SetListInterpretationJobs");

            migrationBuilder.DropColumn(
                name: "SourceEvidenceJson",
                table: "GigSetListItems");
        }
    }
}
