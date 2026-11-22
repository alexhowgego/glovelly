using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftInvoicePdfWatermark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PdfHasDraftWatermark",
                table: "Invoices",
                type: "boolean",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Invoices"
                SET "DocumentRevision" = "DocumentRevision" + 1,
                    "DocumentState" = 'Failed',
                    "DocumentFailureMessage" = 'Regenerate this invoice PDF to add the DRAFT watermark.'
                WHERE "Status" = 'Draft'
                   OR ("Status" = 'Cancelled' AND "FirstIssuedUtc" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfHasDraftWatermark",
                table: "Invoices");
        }
    }
}
