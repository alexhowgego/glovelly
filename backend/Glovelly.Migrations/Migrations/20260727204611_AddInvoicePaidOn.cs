using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePaidOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PaidOn",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Invoices"
                SET "PaidOn" = ("StatusUpdatedUtc" AT TIME ZONE 'Europe/London')::date
                WHERE "Status" = 'Paid' AND "StatusUpdatedUtc" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidOn",
                table: "Invoices");
        }
    }
}
