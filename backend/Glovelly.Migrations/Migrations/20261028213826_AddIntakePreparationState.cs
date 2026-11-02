using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakePreparationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreparationError",
                table: "CurrentIntakes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreparationId",
                table: "CurrentIntakes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreparationStartedAt",
                table: "CurrentIntakes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparationState",
                table: "CurrentIntakes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreparationError",
                table: "CurrentIntakes");

            migrationBuilder.DropColumn(
                name: "PreparationId",
                table: "CurrentIntakes");

            migrationBuilder.DropColumn(
                name: "PreparationStartedAt",
                table: "CurrentIntakes");

            migrationBuilder.DropColumn(
                name: "PreparationState",
                table: "CurrentIntakes");
        }
    }
}
