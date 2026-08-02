using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessRequestReviewLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecisionAtUtc",
                table: "AccessRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                table: "AccessRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProvisionedUserId",
                table: "AccessRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "AccessRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AccessRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_Status_RequestedAtUtc",
                table: "AccessRequests",
                columns: new[] { "Status", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessRequests_Status_RequestedAtUtc",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "DecisionAtUtc",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "ProvisionedUserId",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AccessRequests");
        }
    }
}
