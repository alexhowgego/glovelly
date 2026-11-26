using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasLegacyIssuance",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LicenceChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetIdentity = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorIdentity = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousAssignment = table.Column<string>(type: "text", nullable: true),
                    NewAssignment = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenceChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MonthlyFirstIssueLimit = table.Column<int>(type: "integer", nullable: true),
                    PaidIncomeSummary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Code);
                    table.CheckConstraint("CK_Plans_NonnegativeLimit", "\"MonthlyFirstIssueLimit\" IS NULL OR \"MonthlyFirstIssueLimit\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "UsageLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_UsageLedgerEntries_PositiveQuantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_UsageLedgerEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.UserId);
                    table.CheckConstraint("CK_UserSubscriptions_EndAfterStart", "\"EndsAtUtc\" IS NULL OR \"EndsAtUtc\" > \"StartsAtUtc\"");
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Plans_PlanCode",
                        column: x => x.PlanCode,
                        principalTable: "Plans",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Code", "DisplayName", "MonthlyFirstIssueLimit", "PaidIncomeSummary" },
                values: new object[,]
                {
                    { "free", "Free", 2, false },
                    { "pro", "Pro", null, true },
                    { "solo", "Solo", null, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicenceChanges_TargetUserId_ChangedAtUtc",
                table: "LicenceChanges",
                columns: new[] { "TargetUserId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedgerEntries_Metric_SourceInvoiceId",
                table: "UsageLedgerEntries",
                columns: new[] { "Metric", "SourceInvoiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedgerEntries_UserId_Metric_OccurredAtUtc",
                table: "UsageLedgerEntries",
                columns: new[] { "UserId", "Metric", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PlanCode",
                table: "UserSubscriptions",
                column: "PlanCode");

            // This data rollout runs once under EF migration history, never on login.
            // Keep subscription creation and its identity-snapshot audit transactional.
            migrationBuilder.Sql("""
                INSERT INTO "UserSubscriptions"
                    ("UserId", "PlanCode", "State", "StartsAtUtc", "EndsAtUtc", "Source", "Reason", "Version")
                SELECT "Id", 'pro', 'Active', CURRENT_TIMESTAMP, NULL, 'Manual', 'Founder', gen_random_uuid()
                FROM "Users"
                WHERE NULLIF(BTRIM("GoogleSubject"), '') IS NOT NULL AND "LastLoginUtc" IS NOT NULL;

                INSERT INTO "LicenceChanges"
                    ("Id", "TargetUserId", "TargetIdentity", "ActorUserId", "ActorIdentity",
                     "Source", "ChangedAtUtc", "PreviousAssignment", "NewAssignment", "Reason")
                SELECT gen_random_uuid(), u."Id", u."Email", NULL, 'Founder rollout',
                       'FounderRollout20261122', CURRENT_TIMESTAMP, NULL,
                       json_build_object(
                           'userId', s."UserId", 'planCode', s."PlanCode", 'state', s."State",
                           'startsAtUtc', s."StartsAtUtc", 'endsAtUtc', s."EndsAtUtc",
                           'source', s."Source", 'reason', s."Reason", 'version', s."Version")::text,
                       'Founder'
                FROM "UserSubscriptions" s JOIN "Users" u ON u."Id" = s."UserId";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "UsageLedgerEntries"
                    ("Id", "UserId", "Metric", "SourceInvoiceId", "Quantity", "OccurredAtUtc", "Source")
                SELECT gen_random_uuid(), u."Id", 'InvoiceFirstIssued', i."Id", 1,
                       i."FirstIssuedUtc", 'LegacyBackfill20261122'
                FROM "Invoices" i
                JOIN "Users" u ON u."Id" = COALESCE(i."FirstIssuedByUserId", i."CreatedByUserId")
                WHERE i."FirstIssuedUtc" IS NOT NULL;

                UPDATE "Invoices"
                SET "HasLegacyIssuance" = TRUE
                WHERE "FirstIssuedUtc" IS NULL
                  AND ("Status" IN ('Issued', 'Paid', 'Overdue') OR "ReissueCount" > 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicenceChanges");

            migrationBuilder.DropTable(
                name: "UsageLedgerEntries");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropColumn(
                name: "HasLegacyIssuance",
                table: "Invoices");
        }
    }
}
