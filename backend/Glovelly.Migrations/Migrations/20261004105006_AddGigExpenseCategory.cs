using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGigExpenseCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "GigExpenses",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_GigExpenses_Category",
                table: "GigExpenses",
                sql: "\"Category\" IS NULL OR \"Category\" IN ('Travel', 'Meals', 'Accommodation', 'Equipment', 'Other')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_GigExpenses_Category",
                table: "GigExpenses");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "GigExpenses");
        }
    }
}
