using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCompletedIntakeState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(name: "EvidenceJson", table: "CurrentIntakes", type: "character varying(32768)", maxLength: 32768, nullable: false, oldClrType: typeof(string), oldType: "character varying(4000)", oldMaxLength: 4000);
            migrationBuilder.AlterColumn<string>(name: "EvidenceJson", table: "IntakeAnalysisAttempts", type: "character varying(32768)", maxLength: 32768, nullable: false, oldClrType: typeof(string), oldType: "character varying(4000)", oldMaxLength: 4000);
            migrationBuilder.DropColumn(
                name: "AppliedAt",
                table: "CurrentIntakes");

            migrationBuilder.DropColumn(
                name: "AppliedAttachmentId",
                table: "CurrentIntakes");

            migrationBuilder.DropColumn(
                name: "AppliedExpenseId",
                table: "CurrentIntakes");

            migrationBuilder.DropColumn(
                name: "AppliedGigId",
                table: "CurrentIntakes");

            migrationBuilder.DropColumn(
                name: "AppliedIntent",
                table: "CurrentIntakes");

            migrationBuilder.DropColumn(
                name: "AppliedResourceId",
                table: "CurrentIntakes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(name: "EvidenceJson", table: "CurrentIntakes", type: "character varying(4000)", maxLength: 4000, nullable: false, oldClrType: typeof(string), oldType: "character varying(32768)", oldMaxLength: 32768);
            migrationBuilder.AlterColumn<string>(name: "EvidenceJson", table: "IntakeAnalysisAttempts", type: "character varying(4000)", maxLength: 4000, nullable: false, oldClrType: typeof(string), oldType: "character varying(32768)", oldMaxLength: 32768);
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AppliedAt",
                table: "CurrentIntakes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppliedAttachmentId",
                table: "CurrentIntakes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppliedExpenseId",
                table: "CurrentIntakes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppliedGigId",
                table: "CurrentIntakes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppliedIntent",
                table: "CurrentIntakes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppliedResourceId",
                table: "CurrentIntakes",
                type: "uuid",
                nullable: true);
        }
    }
}
