using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceMutationVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MutationVersion",
                table: "Invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MutationVersion",
                table: "Invoices");
        }
    }
}
