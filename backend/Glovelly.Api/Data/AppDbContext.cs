using Glovelly.Api.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Gig> Gigs => Set<Gig>();
    public DbSet<GigImportBatch> GigImportBatches => Set<GigImportBatch>();
    public DbSet<GigImportDraft> GigImportDrafts => Set<GigImportDraft>();
    public DbSet<GigExpense> GigExpenses => Set<GigExpense>();
    public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();
    public DbSet<GoogleConnection> GoogleConnections => Set<GoogleConnection>();
    public DbSet<GoogleDriveIntegrationSettings> GoogleDriveIntegrationSettings => Set<GoogleDriveIntegrationSettings>();
    public DbSet<McpOAuthAuthorizationCode> McpOAuthAuthorizationCodes => Set<McpOAuthAuthorizationCode>();
    public DbSet<McpOAuthAccessToken> McpOAuthAccessTokens => Set<McpOAuthAccessToken>();
    public DbSet<McpOAuthRefreshToken> McpOAuthRefreshTokens => Set<McpOAuthRefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
