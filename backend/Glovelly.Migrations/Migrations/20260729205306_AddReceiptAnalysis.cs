using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiptAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseAttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Merchant = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    SuggestedCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MerchantConfidence = table.Column<int>(type: "integer", nullable: false),
                    TransactionDateConfidence = table.Column<int>(type: "integer", nullable: false),
                    TotalAmountConfidence = table.Column<int>(type: "integer", nullable: false),
                    CurrencyConfidence = table.Column<int>(type: "integer", nullable: false),
                    SuggestedCategoryConfidence = table.Column<int>(type: "integer", nullable: false),
                    Warnings = table.Column<string>(type: "jsonb", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptAnalyses_ExpenseAttachments_ExpenseAttachmentId",
                        column: x => x.ExpenseAttachmentId,
                        principalTable: "ExpenseAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAnalyses_ExpenseAttachmentId_RequestedAt",
                table: "ReceiptAnalyses",
                columns: new[] { "ExpenseAttachmentId", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptAnalyses");
        }
    }
}
