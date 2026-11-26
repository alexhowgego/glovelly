using System.Data.Common;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class InvoiceIssuancePostgresTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-15T09:00:00Z");

    [PostgresAdministrationFact]
    public async Task ConcurrentLastUnitAllowsOneInvoiceOnly()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var (user, client, invoices) = await SeedAsync(setup, 2);
            setup.UsageLedgerEntries.Add(new UsageLedgerEntry
            {
                Id = Guid.NewGuid(), UserId = user.Id, SourceInvoiceId = Guid.NewGuid(),
                OccurredAtUtc = Now.AddDays(-1), Source = "Test",
            });
            await setup.SaveChangesAsync(token);
            var store = new InMemoryBlobStore();
            var results = await Task.WhenAll(invoices.Select(async invoice =>
            {
                await using var db = Context(setup);
                var loaded = await db.Invoices.Include(value => value.Lines).SingleAsync(value => value.Id == invoice.Id, token);
                try
                {
                    await Workflow(db, store).IssueInvoiceAsync(loaded, client, user.Id, token);
                    return "Issued";
                }
                catch (InvoiceAllowanceExceededException)
                {
                    return "Denied";
                }
            }));
            Assert.Single(results, result => result == "Issued");
            Assert.Single(results, result => result == "Denied");
            Assert.Equal(2, await setup.UsageLedgerEntries.CountAsync(token));
            Assert.Equal(1, await setup.Invoices.CountAsync(value => value.Status == InvoiceStatus.Issued, token));
        });
    }

    [PostgresAdministrationFact]
    public async Task DuplicateSharedInvoiceRequestsCannotDoubleChargeDifferentUsers()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var (user, client, invoices) = await SeedAsync(setup, 1);
            var other = new User { Id = Guid.NewGuid(), Email = "other@example.test", CreatedUtc = Now.UtcDateTime };
            setup.Users.Add(other);
            invoices[0].CreatedByUserId = null;
            await setup.SaveChangesAsync(token);
            var store = new InMemoryBlobStore();
            await using var first = Context(setup);
            await using var second = Context(setup);
            var one = await first.Invoices.SingleAsync(token);
            var two = await second.Invoices.SingleAsync(token);
            async Task<bool> IssueAsync(AppDbContext db, Invoice invoice, Guid userId)
            {
                try
                {
                    await Workflow(db, store).IssueInvoiceAsync(invoice, client, userId, token);
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    return false;
                }
            }
            var outcomes = await Task.WhenAll(IssueAsync(first, one, user.Id), IssueAsync(second, two, other.Id));
            Assert.Single(outcomes, result => result);
            var entry = Assert.Single(await setup.UsageLedgerEntries.AsNoTracking().ToListAsync(token));
            Assert.Equal(invoices[0].Id, entry.SourceInvoiceId);
            Assert.Null((await setup.Invoices.AsNoTracking().SingleAsync(token)).CreatedByUserId);
        });
    }

    [PostgresAdministrationFact]
    public async Task FailedCommitLeavesDraftPointerAndAllowanceUnchanged()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var (user, client, invoices) = await SeedAsync(setup, 1);
            var store = new InMemoryBlobStore();
            var pdfService = new InvoicePdfService(store, new Clock());
            await pdfService.SaveGeneratedPdfAsync(invoices[0], user.Id, [1, 2, 3], true, token);
            await setup.SaveChangesAsync(token);
            var original = invoices[0].PdfStorageKey;
            await using (var failing = Context(setup, new FailCommit()))
            {
                var invoice = await failing.Invoices.SingleAsync(token);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Workflow(failing, store).IssueInvoiceAsync(invoice, client, user.Id, token));
            }
            await using var verify = Context(setup);
            var retained = await verify.Invoices.SingleAsync(token);
            Assert.Equal(InvoiceStatus.Draft, retained.Status);
            Assert.Null(retained.FirstIssuedUtc);
            Assert.Equal(original, retained.PdfStorageKey);
            Assert.Empty(await verify.UsageLedgerEntries.ToListAsync(token));
            var read = await pdfService.OpenCurrentReadAsync(retained, token);
            Assert.True(read.IsAvailable);
            using var bytes = new MemoryStream();
            await using (read.Pdf!.Content)
            {
                await read.Pdf.Content.CopyToAsync(bytes, token);
            }
            Assert.Equal(new byte[] { 1, 2, 3 }, bytes.ToArray());
        });
    }

    [PostgresAdministrationFact]
    public async Task IssueRechecksPlanAfterWaitingForConcurrentLicenceGrant()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var (user, client, invoices) = await SeedAsync(setup, 1);
            setup.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
            {
                UserId = user.Id, PermissionSet = AdministrativePermissionSet.LicenseAdministrator,
            });
            setup.UsageLedgerEntries.Add(new UsageLedgerEntry
            {
                Id = Guid.NewGuid(), UserId = user.Id, SourceInvoiceId = Guid.NewGuid(),
                Quantity = 2, OccurredAtUtc = Now, Source = "Test",
            });
            await setup.SaveChangesAsync(token);
            var pause = new PauseAfterSave();
            await using var licensing = Context(setup, pause);
            var service = new LicenceAssignmentService(licensing, new AdministrativeMutationCoordinator(licensing),
                new EntitlementMutationCoordinator(licensing), new Clock());
            var grant = service.AssignAsync(user.Id, new(Plan.Pro, null, false, null, "Tester"), user.Id, token);
            await pause.Saved.Task.WaitAsync(token);
            await using var issuing = Context(setup);
            var loaded = await issuing.Invoices.SingleAsync(value => value.Id == invoices[0].Id, token);
            var issue = Workflow(issuing, new InMemoryBlobStore()).IssueInvoiceAsync(loaded, client, user.Id, token);
            pause.Release.TrySetResult();
            Assert.Equal(LicenceAssignmentResult.Changed, await grant);
            await issue;
            Assert.Equal(3, await setup.UsageLedgerEntries.SumAsync(value => value.Quantity, token));
        });
    }

    [PostgresAdministrationFact]
    public async Task UncertainOutcomeAfterActualCommitRetainsPublishedBytesAndUsage()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var (user, client, _) = await SeedAsync(setup, 1);
            var store = new RecordingStore();
            await using (var failing = Context(setup, new FailAfterCommit()))
            {
                var invoice = await failing.Invoices.SingleAsync(token);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Workflow(failing, store).IssueInvoiceAsync(invoice, client, user.Id, token));
            }
            await using var verify = Context(setup);
            var persisted = await verify.Invoices.SingleAsync(token);
            Assert.Equal(InvoiceStatus.Issued, persisted.Status);
            Assert.Single(await verify.UsageLedgerEntries.ToListAsync(token));
            Assert.Empty(store.Deleted);
            Assert.True((await new InvoicePdfService(store, new Clock()).OpenCurrentReadAsync(persisted, token)).IsAvailable);
        });
    }

    [PostgresAdministrationFact]
    public async Task RenderStorageAndSaveFailuresNeverPublishCleanOutput()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var (user, client, invoices) = await SeedAsync(setup, 1);
            var store = new RecordingStore();
            await new InvoicePdfService(store, new Clock()).SaveGeneratedPdfAsync(
                invoices[0], user.Id, [1, 2, 3], true, token);
            await setup.SaveChangesAsync(token);
            var originalKey = invoices[0].PdfStorageKey;
            foreach (var stage in new[] { "render", "storage", "save" })
            {
                await using var failing = stage == "save" ? Context(setup, new FailSave()) : Context(setup);
                store.FailSave = stage == "storage";
                var invoice = await failing.Invoices.SingleAsync(token);
                var workflow = Workflow(failing, store, stage == "render" ? new FailRenderer() : null);
                await Assert.ThrowsAnyAsync<Exception>(() => workflow.IssueInvoiceAsync(invoice, client, user.Id, token));
                await using var verify = Context(setup);
                var retained = await verify.Invoices.SingleAsync(token);
                Assert.Equal(InvoiceStatus.Draft, retained.Status);
                Assert.Null(retained.FirstIssuedUtc);
                Assert.Equal(originalKey, retained.PdfStorageKey);
                Assert.Empty(await verify.UsageLedgerEntries.ToListAsync(token));
            }
            // Only the known pre-commit save failure has a stored candidate to
            // discard; the committed draft must never be cleaned up.
            Assert.Single(store.Deleted);
            Assert.NotEqual(originalKey, store.Deleted[0]);
        });
    }

    [PostgresAdministrationFact]
    public async Task LineEditInvalidatesParentAndRejectsStalePublication()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var (user, client, _) = await SeedAsync(setup, 1);
            await using var stale = Context(setup);
            var invoice = await stale.Invoices.Include(value => value.Lines).SingleAsync(token);
            await using (var editing = Context(setup))
            {
                editing.InvoiceLines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(), InvoiceId = invoice.Id, Description = "Concurrent edit",
                    Quantity = 1, UnitPrice = 50,
                });
                await editing.SaveChangesAsync(token);
            }
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                Workflow(stale, new InMemoryBlobStore()).IssueInvoiceAsync(invoice, client, user.Id, token));
            Assert.Empty(await setup.UsageLedgerEntries.AsNoTracking().ToListAsync(token));
        });
    }

    private static AppDbContext Context(AppDbContext setup, params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(setup.Database.GetConnectionString()).AddInterceptors(interceptors).Options);

    private static async Task<(User User, Client Client, Invoice[] Invoices)> SeedAsync(AppDbContext db, int count)
    {
        var token = TestContext.Current.CancellationToken;
        await db.Database.MigrateAsync(token);
        var user = new User { Id = Guid.NewGuid(), Email = "issuer@example.test", CreatedUtc = Now.UtcDateTime };
        var client = new Client { Id = Guid.NewGuid(), Name = "Issuance client" };
        var invoices = Enumerable.Range(0, count).Select(index => new Invoice
        {
            Id = Guid.NewGuid(), InvoiceNumber = $"TEST-{index}", ClientId = client.Id,
            CreatedByUserId = user.Id, InvoiceDate = new DateOnly(2026, 7, 1), DueDate = new DateOnly(2026, 7, 15),
        }).ToArray();
        db.Users.Add(user);
        db.Clients.Add(client);
        db.Invoices.AddRange(invoices);
        await db.SaveChangesAsync(token);
        return (user, client, invoices);
    }

    private static InvoiceWorkflowService Workflow(AppDbContext db, IBlobStore store, IInvoicePdfRenderer? renderer = null) => new(
        db, new InvoiceNumberService(db),
        new InvoiceLineGenerationService(db, Options.Create(new InvoiceRateSettings())),
        new InvoiceProfileDefaultsService(db), renderer ?? new InvoicePdfRenderer(),
        new InvoicePdfService(store, new Clock()), new EntitlementService(db, new Clock()),
        new EntitlementMutationCoordinator(db), new Clock());

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FailCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected failure before database commit.");
    }

    private sealed class FailSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Injected failure before save.");
    }

    private sealed class FailAfterCommit : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(
            DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected lost acknowledgement after actual commit.");
    }

    private sealed class PauseAfterSave : SaveChangesInterceptor
    {
        public TaskCompletionSource Saved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Saved.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class FailRenderer : IInvoicePdfRenderer
    {
        public byte[] RenderInvoicePdf(Invoice invoice, Client client, Gig? gig,
            IReadOnlyCollection<InvoiceLine> lines, SellerProfile? sellerProfile) =>
            throw new InvalidOperationException("Injected renderer failure.");
    }

    private sealed class RecordingStore : IBlobStore
    {
        private readonly InMemoryBlobStore _inner = new();
        public bool FailSave { get; set; }
        public List<string> Deleted { get; } = [];
        public Task SaveAsync(BlobWriteRequest request, CancellationToken cancellationToken = default) =>
            FailSave ? throw new InvalidOperationException("Injected storage failure.") : _inner.SaveAsync(request, cancellationToken);
        public Task<BlobReadResult> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
            _inner.OpenReadAsync(key, cancellationToken);
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            Deleted.Add(key);
            return _inner.DeleteAsync(key, cancellationToken);
        }
    }
}
