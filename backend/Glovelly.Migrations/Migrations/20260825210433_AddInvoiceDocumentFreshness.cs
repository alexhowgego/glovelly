using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDocumentFreshness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentFailureMessage",
                table: "Invoices",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentRevision",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "DocumentState",
                table: "Invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Missing");

            migrationBuilder.AddColumn<int>(
                name: "PdfDocumentRevision",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Invoices"
                SET "DocumentState" = 'Current',
                    "PdfDocumentRevision" = "DocumentRevision"
                WHERE "PdfStorageKey" IS NOT NULL
                  AND btrim("PdfStorageKey") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentFailureMessage",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DocumentRevision",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DocumentState",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PdfDocumentRevision",
                table: "Invoices");
        }
    }
}
