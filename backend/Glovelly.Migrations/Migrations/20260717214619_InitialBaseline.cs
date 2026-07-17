using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Glovelly.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestIpHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    NotificationSentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotificationSuppressionReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MileageRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PassengerMileageRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TravelOriginPostcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultPaymentWindowDays = table.Column<int>(type: "integer", nullable: true),
                    InvoiceFilenamePattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceEmailSubjectPattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceEmailBodyTemplate = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InvoiceReplyToEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarSyncWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GigId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessingOwnerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProcessingStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastErrorType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LastErrorDetail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSyncWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSyncWorkItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    BillingAddress_Line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BillingAddress_Line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillingAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BillingAddress_StateOrCounty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BillingAddress_PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BillingAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MileageRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PassengerMileageRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    InvoiceFilenamePattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceEmailSubjectPattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Clients_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForScoreLibrarySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BackupVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ChartCount = table.Column<int>(type: "integer", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForScoreLibrarySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForScoreLibrarySnapshots_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GigCalendarSyncStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GigId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderCalendarId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ProviderEventId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LastSyncHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastSyncAttemptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigCalendarSyncStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GigCalendarSyncStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GigImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GigImportBatches_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoogleConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleSubject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GoogleEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    EncryptedAccessToken = table.Column<string>(type: "text", nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "text", nullable: false),
                    AccessTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RefreshTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GrantedScopes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TokenType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConnectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpOAuthAccessTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Resource = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpOAuthAccessTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpOAuthAccessTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpOAuthAuthorizationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Scope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Resource = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CodeChallenge = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CodeChallengeMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpOAuthAuthorizationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpOAuthAuthorizationCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpOAuthRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Resource = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpOAuthRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpOAuthRefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SellerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address_Line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address_Line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Address_StateOrCounty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Address_PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PaymentReferenceNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SellerProfiles_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerProfiles_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StatusUpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstIssuedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstIssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReissueCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastReissuedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReissuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastDeliveryChannel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastDeliveryRecipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    LastDeliveredUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastDeliveredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PdfStorageKey = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    PdfFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PdfContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PdfSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    PdfGeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForScoreCharts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ForScoreLibrarySnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Keywords = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PrintNumber = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForScoreCharts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForScoreCharts_ForScoreLibrarySnapshots_ForScoreLibrarySnap~",
                        column: x => x.ForScoreLibrarySnapshotId,
                        principalTable: "ForScoreLibrarySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GigImportDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposedClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProposedContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProposedContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ProposedProjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProposedTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProposedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProposedArrivalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ProposedRehearsalStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ProposedRehearsalEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ProposedShowStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ProposedShowEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ProposedVenueName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProposedVenueAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProposedVenuePostcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ProposedFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ProposedPerDiem = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ProposedNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AccommodationNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TravelNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Confidence = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigImportDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GigImportDrafts_Clients_ProposedClientId",
                        column: x => x.ProposedClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GigImportDrafts_GigImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "GigImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoogleCalendarIntegrationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GoogleCalendarId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CalendarName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastSuccessfulSyncAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IncludeLocation = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleCalendarIntegrationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleCalendarIntegrationSettings_GoogleConnections_GoogleC~",
                        column: x => x.GoogleConnectionId,
                        principalTable: "GoogleConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoogleCalendarIntegrationSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoogleDriveIntegrationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceUploadFolderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleDriveIntegrationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleDriveIntegrationSettings_GoogleConnections_GoogleConn~",
                        column: x => x.GoogleConnectionId,
                        principalTable: "GoogleConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoogleDriveIntegrationSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Gigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceImportDraftId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Venue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TravelMiles = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PassengerCount = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    WasDriving = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoicedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gigs_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gigs_GigImportBatches_SourceImportBatchId",
                        column: x => x.SourceImportBatchId,
                        principalTable: "GigImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Gigs_GigImportDrafts_SourceImportDraftId",
                        column: x => x.SourceImportDraftId,
                        principalTable: "GigImportDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Gigs_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Gigs_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gigs_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GigExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GigId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReimbursementUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReimbursementInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReimbursementStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Unreimbursed"),
                    ReimbursedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReimbursementUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReimbursementMethod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReimbursementNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GigExpenses_Gigs_GigId",
                        column: x => x.GigId,
                        principalTable: "Gigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GigExpenses_Invoices_ReimbursementInvoiceId",
                        column: x => x.ReimbursementInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GigExpenses_Users_ReimbursementUpdatedByUserId",
                        column: x => x.ReimbursementUpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GigExternalResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GigId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigExternalResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GigExternalResources_Gigs_GigId",
                        column: x => x.GigId,
                        principalTable: "Gigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GigId = table.Column<Guid>(type: "uuid", nullable: true),
                    CalculationNotes = table.Column<string>(type: "text", nullable: true),
                    IsSystemGenerated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Gigs_GigId",
                        column: x => x.GigId,
                        principalTable: "Gigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetListChartMatchJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GigId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InputJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    SafeErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetListChartMatchJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetListChartMatchJobs_Gigs_GigId",
                        column: x => x.GigId,
                        principalTable: "Gigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SetListChartMatchJobs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GigExpenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseAttachments_GigExpenses_GigExpenseId",
                        column: x => x.GigExpenseId,
                        principalTable: "GigExpenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GigExternalResourceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GigExternalResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigExternalResourceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GigExternalResourceAttachments_GigExternalResources_GigExte~",
                        column: x => x.GigExternalResourceId,
                        principalTable: "GigExternalResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GigSetListImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GigId = table.Column<Guid>(type: "uuid", nullable: false),
                    GigExternalResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpreadsheetId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorksheetId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WorksheetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigSetListImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GigSetListImports_GigExternalResources_GigExternalResourceId",
                        column: x => x.GigExternalResourceId,
                        principalTable: "GigExternalResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GigSetListImports_Gigs_GigId",
                        column: x => x.GigId,
                        principalTable: "Gigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GigSetListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GigSetListImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    SourceRowNumber = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Include = table.Column<bool>(type: "boolean", nullable: false),
                    Section = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PadNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RawCellsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Confidence = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ForScoreLibrarySnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    ForScoreChartId = table.Column<Guid>(type: "uuid", nullable: true),
                    ForScoreChartTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ForScoreChartFilePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ForScoreMappingStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Unmapped"),
                    ForScoreMappingConfidence = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "None"),
                    ForScoreMappingUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ForScoreMatchJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigSetListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GigSetListItems_ForScoreCharts_ForScoreChartId",
                        column: x => x.ForScoreChartId,
                        principalTable: "ForScoreCharts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GigSetListItems_ForScoreLibrarySnapshots_ForScoreLibrarySna~",
                        column: x => x.ForScoreLibrarySnapshotId,
                        principalTable: "ForScoreLibrarySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GigSetListItems_GigSetListImports_GigSetListImportId",
                        column: x => x.GigSetListImportId,
                        principalTable: "GigSetListImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_NormalizedEmail",
                table: "AccessRequests",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_NotificationSentAtUtc",
                table: "AccessRequests",
                column: "NotificationSentAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_RequestedAtUtc",
                table: "AccessRequests",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSyncWorkItems_GigId_Provider",
                table: "CalendarSyncWorkItems",
                columns: new[] { "GigId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSyncWorkItems_ProcessingOwnerId",
                table: "CalendarSyncWorkItems",
                column: "ProcessingOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSyncWorkItems_Status_NextAttemptAtUtc",
                table: "CalendarSyncWorkItems",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSyncWorkItems_Status_ProcessingStartedAtUtc",
                table: "CalendarSyncWorkItems",
                columns: new[] { "Status", "ProcessingStartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSyncWorkItems_UserId_Provider",
                table: "CalendarSyncWorkItems",
                columns: new[] { "UserId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CreatedByUserId",
                table: "Clients",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_UpdatedByUserId",
                table: "Clients",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAttachments_GigExpenseId",
                table: "ExpenseAttachments",
                column: "GigExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAttachments_StorageKey",
                table: "ExpenseAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForScoreCharts_ForScoreLibrarySnapshotId",
                table: "ForScoreCharts",
                column: "ForScoreLibrarySnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ForScoreCharts_ForScoreLibrarySnapshotId_NormalizedTitle",
                table: "ForScoreCharts",
                columns: new[] { "ForScoreLibrarySnapshotId", "NormalizedTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_ForScoreCharts_ForScoreLibrarySnapshotId_SortOrder",
                table: "ForScoreCharts",
                columns: new[] { "ForScoreLibrarySnapshotId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ForScoreLibrarySnapshots_CreatedByUserId",
                table: "ForScoreLibrarySnapshots",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ForScoreLibrarySnapshots_CreatedByUserId_IsActive",
                table: "ForScoreLibrarySnapshots",
                columns: new[] { "CreatedByUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GigCalendarSyncStates_GigId_Provider",
                table: "GigCalendarSyncStates",
                columns: new[] { "GigId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GigCalendarSyncStates_ProviderCalendarId_ProviderEventId",
                table: "GigCalendarSyncStates",
                columns: new[] { "ProviderCalendarId", "ProviderEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_GigCalendarSyncStates_UserId_Provider",
                table: "GigCalendarSyncStates",
                columns: new[] { "UserId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_GigExpenses_GigId",
                table: "GigExpenses",
                column: "GigId");

            migrationBuilder.CreateIndex(
                name: "IX_GigExpenses_ReimbursementInvoiceId",
                table: "GigExpenses",
                column: "ReimbursementInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_GigExpenses_ReimbursementUpdatedByUserId",
                table: "GigExpenses",
                column: "ReimbursementUpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GigExternalResourceAttachments_GigExternalResourceId",
                table: "GigExternalResourceAttachments",
                column: "GigExternalResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_GigExternalResourceAttachments_StorageKey",
                table: "GigExternalResourceAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GigExternalResources_GigId",
                table: "GigExternalResources",
                column: "GigId");

            migrationBuilder.CreateIndex(
                name: "IX_GigImportBatches_CreatedByUserId",
                table: "GigImportBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GigImportBatches_SourceFingerprint",
                table: "GigImportBatches",
                column: "SourceFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_GigImportBatches_Status",
                table: "GigImportBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GigImportDrafts_BatchId",
                table: "GigImportDrafts",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GigImportDrafts_ProposedClientId",
                table: "GigImportDrafts",
                column: "ProposedClientId");

            migrationBuilder.CreateIndex(
                name: "IX_GigImportDrafts_Status",
                table: "GigImportDrafts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Gigs_ClientId",
                table: "Gigs",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Gigs_CreatedByUserId",
                table: "Gigs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Gigs_InvoiceId",
                table: "Gigs",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Gigs_SourceImportBatchId",
                table: "Gigs",
                column: "SourceImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Gigs_SourceImportDraftId",
                table: "Gigs",
                column: "SourceImportDraftId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gigs_UpdatedByUserId",
                table: "Gigs",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GigSetListImports_GigExternalResourceId",
                table: "GigSetListImports",
                column: "GigExternalResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_GigSetListImports_GigId",
                table: "GigSetListImports",
                column: "GigId");

            migrationBuilder.CreateIndex(
                name: "IX_GigSetListImports_GigId_IsActive",
                table: "GigSetListImports",
                columns: new[] { "GigId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GigSetListItems_ForScoreChartId",
                table: "GigSetListItems",
                column: "ForScoreChartId");

            migrationBuilder.CreateIndex(
                name: "IX_GigSetListItems_ForScoreLibrarySnapshotId",
                table: "GigSetListItems",
                column: "ForScoreLibrarySnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_GigSetListItems_ForScoreMappingStatus",
                table: "GigSetListItems",
                column: "ForScoreMappingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_GigSetListItems_GigSetListImportId",
                table: "GigSetListItems",
                column: "GigSetListImportId");

            migrationBuilder.CreateIndex(
                name: "IX_GigSetListItems_GigSetListImportId_SortOrder",
                table: "GigSetListItems",
                columns: new[] { "GigSetListImportId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarIntegrationSettings_GoogleConnectionId",
                table: "GoogleCalendarIntegrationSettings",
                column: "GoogleConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarIntegrationSettings_UserId",
                table: "GoogleCalendarIntegrationSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleConnections_UserId",
                table: "GoogleConnections",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleDriveIntegrationSettings_GoogleConnectionId",
                table: "GoogleDriveIntegrationSettings",
                column: "GoogleConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleDriveIntegrationSettings_UserId",
                table: "GoogleDriveIntegrationSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_GigId",
                table: "InvoiceLines",
                column: "GigId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientId",
                table: "Invoices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedByUserId",
                table: "Invoices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PdfStorageKey",
                table: "Invoices",
                column: "PdfStorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UpdatedByUserId",
                table: "Invoices",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthAccessTokens_ExpiresUtc",
                table: "McpOAuthAccessTokens",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthAccessTokens_TokenHash",
                table: "McpOAuthAccessTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthAccessTokens_UserId",
                table: "McpOAuthAccessTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthAuthorizationCodes_CodeHash",
                table: "McpOAuthAuthorizationCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthAuthorizationCodes_ExpiresUtc",
                table: "McpOAuthAuthorizationCodes",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthAuthorizationCodes_UserId",
                table: "McpOAuthAuthorizationCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthRefreshTokens_ExpiresUtc",
                table: "McpOAuthRefreshTokens",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthRefreshTokens_TokenHash",
                table: "McpOAuthRefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthRefreshTokens_UserId",
                table: "McpOAuthRefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerProfiles_CreatedByUserId",
                table: "SellerProfiles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerProfiles_UpdatedByUserId",
                table: "SellerProfiles",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerProfiles_UserId",
                table: "SellerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetListChartMatchJobs_CorrelationId",
                table: "SetListChartMatchJobs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SetListChartMatchJobs_GigId",
                table: "SetListChartMatchJobs",
                column: "GigId");

            migrationBuilder.CreateIndex(
                name: "IX_SetListChartMatchJobs_Status_CreatedAtUtc",
                table: "SetListChartMatchJobs",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SetListChartMatchJobs_UserId_GigId_CreatedAtUtc",
                table: "SetListChartMatchJobs",
                columns: new[] { "UserId", "GigId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleSubject",
                table: "Users",
                column: "GoogleSubject",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessRequests");

            migrationBuilder.DropTable(
                name: "CalendarSyncWorkItems");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "ExpenseAttachments");

            migrationBuilder.DropTable(
                name: "GigCalendarSyncStates");

            migrationBuilder.DropTable(
                name: "GigExternalResourceAttachments");

            migrationBuilder.DropTable(
                name: "GigSetListItems");

            migrationBuilder.DropTable(
                name: "GoogleCalendarIntegrationSettings");

            migrationBuilder.DropTable(
                name: "GoogleDriveIntegrationSettings");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "McpOAuthAccessTokens");

            migrationBuilder.DropTable(
                name: "McpOAuthAuthorizationCodes");

            migrationBuilder.DropTable(
                name: "McpOAuthRefreshTokens");

            migrationBuilder.DropTable(
                name: "SellerProfiles");

            migrationBuilder.DropTable(
                name: "SetListChartMatchJobs");

            migrationBuilder.DropTable(
                name: "GigExpenses");

            migrationBuilder.DropTable(
                name: "ForScoreCharts");

            migrationBuilder.DropTable(
                name: "GigSetListImports");

            migrationBuilder.DropTable(
                name: "GoogleConnections");

            migrationBuilder.DropTable(
                name: "ForScoreLibrarySnapshots");

            migrationBuilder.DropTable(
                name: "GigExternalResources");

            migrationBuilder.DropTable(
                name: "Gigs");

            migrationBuilder.DropTable(
                name: "GigImportDrafts");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "GigImportBatches");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
