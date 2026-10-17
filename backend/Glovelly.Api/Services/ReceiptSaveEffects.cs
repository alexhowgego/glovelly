using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class ReceiptSaveEffects(AppDbContext db, IInvoiceWorkflowService invoices, IExpenseAttachmentStore store, IWorkspaceEventPublisher events, TimeProvider clock, ILogger<ReceiptSaveEffects> logger)
{
    public Task PrepareAsync(IReadOnlyCollection<Gig> gigs, Guid? userId, CancellationToken token = default) => invoices.MarkDraftGigChangesAsync(gigs, userId, token);

    public async Task<IReadOnlyList<Invoice>> CompleteAsync(List<Gig> gigs, Guid? userId, IEnumerable<string> cleanupKeys, bool refreshInvoices = true)
    {
        IReadOnlyList<Invoice> refreshed = [];
        if (refreshInvoices)
        {
            try { refreshed = await invoices.RefreshDraftInvoicesForGigsAsync(gigs, userId); }
            catch (Exception)
            {
                logger.LogWarning("Receipt invoice refresh remains pending for user {UserId}.", userId);
                db.ChangeTracker.Clear();
                var ids = gigs.Where(gig => gig.InvoiceId.HasValue).Select(gig => gig.InvoiceId!.Value).ToList();
                refreshed = await db.Invoices.WhereVisibleTo(userId).AsNoTracking().Include(value => value.Lines).Where(value => ids.Contains(value.Id)).ToListAsync();
            }
        }
        foreach (var key in cleanupKeys)
        {
            try { await store.DeleteAsync(key); }
            catch (Exception) { logger.LogWarning("Post-save attachment cleanup remains pending for user {UserId}.", userId); }
        }
        foreach (var gig in gigs) await PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gig.Id, clock.GetUtcNow()));
        foreach (var invoice in refreshed) await PublishAsync(userId, new WorkspaceEvent("invoices", "updated", invoice.Id, clock.GetUtcNow()));
        return refreshed;
    }

    private async Task PublishAsync(Guid? userId, WorkspaceEvent value)
    {
        try { await events.PublishAsync(userId, value); }
        catch (Exception) { logger.LogWarning("Receipt event publication did not complete for {EntityId}.", value.EntityId); }
    }
}
