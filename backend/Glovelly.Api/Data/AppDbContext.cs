using Glovelly.Api.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<LicenceChange> LicenceChanges => Set<LicenceChange>();
    public DbSet<UsageLedgerEntry> UsageLedgerEntries => Set<UsageLedgerEntry>();
    public DbSet<UserAdministrativePermissionSet> UserAdministrativePermissionSets => Set<UserAdministrativePermissionSet>();
    public DbSet<AdministrativePermissionChange> AdministrativePermissionChanges => Set<AdministrativePermissionChange>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Gig> Gigs => Set<Gig>();
    public DbSet<GigImportBatch> GigImportBatches => Set<GigImportBatch>();
    public DbSet<GigImportDraft> GigImportDrafts => Set<GigImportDraft>();
    public DbSet<GigExpense> GigExpenses => Set<GigExpense>();
    public DbSet<GigExternalResource> GigExternalResources => Set<GigExternalResource>();
    public DbSet<GigExternalResourceAttachment> GigExternalResourceAttachments => Set<GigExternalResourceAttachment>();
    public DbSet<GigSetListImport> GigSetListImports => Set<GigSetListImport>();
    public DbSet<GigSetListItem> GigSetListItems => Set<GigSetListItem>();
    public DbSet<SetListChartMatchJob> SetListChartMatchJobs => Set<SetListChartMatchJob>();
    public DbSet<SetListInterpretationJob> SetListInterpretationJobs => Set<SetListInterpretationJob>();
    public DbSet<ForScoreLibrarySnapshot> ForScoreLibrarySnapshots => Set<ForScoreLibrarySnapshot>();
    public DbSet<ForScoreChart> ForScoreCharts => Set<ForScoreChart>();
    public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>();
    public DbSet<ReceiptAnalysis> ReceiptAnalyses => Set<ReceiptAnalysis>();
    public DbSet<CurrentIntake> CurrentIntakes => Set<CurrentIntake>();
    public DbSet<IntakeAnalysisAttempt> IntakeAnalysisAttempts => Set<IntakeAnalysisAttempt>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();
    public DbSet<GoogleConnection> GoogleConnections => Set<GoogleConnection>();
    public DbSet<GoogleDriveIntegrationSettings> GoogleDriveIntegrationSettings => Set<GoogleDriveIntegrationSettings>();
    public DbSet<GoogleCalendarIntegrationSettings> GoogleCalendarIntegrationSettings => Set<GoogleCalendarIntegrationSettings>();
    public DbSet<GigCalendarSyncState> GigCalendarSyncStates => Set<GigCalendarSyncState>();
    public DbSet<CalendarSyncWorkItem> CalendarSyncWorkItems => Set<CalendarSyncWorkItem>();
    public DbSet<McpOAuthAuthorizationCode> McpOAuthAuthorizationCodes => Set<McpOAuthAuthorizationCode>();
    public DbSet<McpOAuthAccessToken> McpOAuthAccessTokens => Set<McpOAuthAccessToken>();
    public DbSet<McpOAuthRefreshToken> McpOAuthRefreshTokens => Set<McpOAuthRefreshToken>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareInvoiceMutationsAsync(false, CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await PrepareInvoiceMutationsAsync(true, cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task PrepareInvoiceMutationsAsync(bool asynchronous, CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();
        // Every line writer participates, including generic line routes and
        // background gig refresh. A move invalidates both source and destination.
        var lineParents = ChangeTracker.Entries<InvoiceLine>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .SelectMany(entry => new[]
            {
                entry.Entity.InvoiceId,
                entry.Property(value => value.InvoiceId).OriginalValue,
            }).Where(id => id != Guid.Empty).Distinct().ToList();
        foreach (var id in lineParents)
        {
            var invoice = Invoices.Local.FirstOrDefault(value => value.Id == id);
            if (invoice is null)
            {
                invoice = asynchronous
                    ? await Invoices.SingleOrDefaultAsync(value => value.Id == id, cancellationToken)
                    : Invoices.SingleOrDefault(value => value.Id == id);
            }
            if (invoice is null || Entry(invoice).State is EntityState.Added or EntityState.Deleted)
            {
                continue;
            }
            var entry = Entry(invoice);
            // A newly prepared candidate already includes the pending lines.
            // Otherwise the existing PDF must no longer appear current.
            if (!entry.Property(value => value.PdfStorageKey).IsModified)
            {
                if (!entry.Property(value => value.DocumentRevision).IsModified)
                {
                    invoice.DocumentRevision++;
                }
                invoice.DocumentState = InvoiceDocumentState.Regenerating;
                invoice.DocumentFailureMessage = null;
            }
            entry.Property(value => value.MutationVersion).IsModified = true;
        }
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<Invoice>()
            .Where(entry => entry.State == EntityState.Modified))
        {
            entry.Entity.MutationVersion = Guid.NewGuid();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        var setListJobEntity = modelBuilder.Entity<SetListChartMatchJob>().Metadata;
        var gigIdProperty = setListJobEntity.FindProperty(nameof(SetListChartMatchJob.GigId));
        if (gigIdProperty is not null)
        {
            var gigIdIndexProperties = new[] { gigIdProperty };
            if (setListJobEntity.FindIndex(gigIdIndexProperties) is not null)
            {
                setListJobEntity.RemoveIndex(gigIdIndexProperties);
            }
        }
    }
}
