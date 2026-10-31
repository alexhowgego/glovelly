using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGigIntakeProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceContentType",
                table: "GigImportBatches",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceEvidenceJson",
                table: "GigImportBatches",
                type: "character varying(32768)",
                maxLength: 32768,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceIntakeId",
                table: "GigImportBatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceStorageKey",
                table: "GigImportBatches",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "GigImportBatches",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "GigImportBatches",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GigImportBatches_CreatedByUserId_SourceIntakeId",
                table: "GigImportBatches",
                columns: new[] { "CreatedByUserId", "SourceIntakeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GigImportBatches_CreatedByUserId_SourceIntakeId",
                table: "GigImportBatches");

            migrationBuilder.DropColumn(
                name: "SourceContentType",
                table: "GigImportBatches");

            migrationBuilder.DropColumn(
                name: "SourceEvidenceJson",
                table: "GigImportBatches");

            migrationBuilder.DropColumn(
                name: "SourceIntakeId",
                table: "GigImportBatches");

            migrationBuilder.DropColumn(
                name: "SourceStorageKey",
                table: "GigImportBatches");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "GigImportBatches");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "GigImportBatches");
        }
    }
}
