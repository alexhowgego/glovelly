using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CapabilityAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdministrativePermissionChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetIdentity = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorIdentity = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousSets = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NewSets = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrativePermissionChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAdministrativePermissionSets",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionSet = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAdministrativePermissionSets", x => new { x.UserId, x.PermissionSet });
                    table.ForeignKey(
                        name: "FK_UserAdministrativePermissionSets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativePermissionChanges_TargetUserId_ChangedAtUtc",
                table: "AdministrativePermissionChanges",
                columns: new[] { "TargetUserId", "ChangedAtUtc" });

            migrationBuilder.Sql("""
                INSERT INTO "UserAdministrativePermissionSets" ("UserId", "PermissionSet")
                SELECT "Id", 'UserAdministrator' FROM "Users" WHERE "Role" = 'Admin';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministrativePermissionChanges");

            migrationBuilder.DropTable(
                name: "UserAdministrativePermissionSets");
        }
    }
}
