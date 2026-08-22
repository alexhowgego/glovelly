using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarSyncFailureSupersession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresReconnection",
                table: "GoogleCalendarIntegrationSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SupersededAtUtc",
                table: "CalendarSyncWorkItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSyncWorkItems_UserId_Provider_Status_SupersededAtUt~",
                table: "CalendarSyncWorkItems",
                columns: new[] { "UserId", "Provider", "Status", "SupersededAtUtc", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CalendarSyncWorkItems_UserId_Provider_Status_SupersededAtUt~",
                table: "CalendarSyncWorkItems");

            migrationBuilder.DropColumn(
                name: "RequiresReconnection",
                table: "GoogleCalendarIntegrationSettings");

            migrationBuilder.DropColumn(
                name: "SupersededAtUtc",
                table: "CalendarSyncWorkItems");
        }
    }
}
