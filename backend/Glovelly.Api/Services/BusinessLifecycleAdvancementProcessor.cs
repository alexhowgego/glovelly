using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class BusinessLifecycleAdvancementProcessor(
    AppDbContext dbContext,
    ICalendarSyncWorkQueue calendarSyncWorkQueue) : IBusinessLifecycleAdvancementProcessor
{
    public async Task<BusinessLifecycleAdvancementResult> AdvanceAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);
        var gigsToComplete = await dbContext.Gigs
            .Where(gig => gig.Status == GigStatus.Confirmed && gig.Date < today)
            .ToListAsync(cancellationToken);
        var invoicesToMarkOverdue = await dbContext.Invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Issued && invoice.DueDate < today)
            .ToListAsync(cancellationToken);

        foreach (var gig in gigsToComplete)
        {
            gig.Status = GigStatus.Completed;
        }

        foreach (var invoice in invoicesToMarkOverdue)
        {
            invoice.Status = InvoiceStatus.Overdue;
            invoice.StatusUpdatedUtc = nowUtc;
        }

        if (gigsToComplete.Count > 0 || invoicesToMarkOverdue.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var gig in gigsToComplete.Where(gig => gig.CreatedByUserId.HasValue))
        {
            var userId = gig.CreatedByUserId!.Value;
            await calendarSyncWorkQueue.EnqueueGigAsync(
                userId,
                gig.Id,
                CalendarSyncWorkItemReason.GigUpdated,
                cancellationToken);
        }

        var nextConfirmedGigDate = await dbContext.Gigs
            .Where(gig => gig.Status == GigStatus.Confirmed)
            .Select(gig => (DateOnly?)gig.Date)
            .MinAsync(cancellationToken);
        var nextIssuedInvoiceDueDate = await dbContext.Invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Issued)
            .Select(invoice => (DateOnly?)invoice.DueDate)
            .MinAsync(cancellationToken);

        return new BusinessLifecycleAdvancementResult(
            gigsToComplete.Count,
            invoicesToMarkOverdue.Count,
            nextConfirmedGigDate.HasValue ? GetTransitionDate(nextConfirmedGigDate.Value) : null,
            nextIssuedInvoiceDueDate.HasValue ? GetTransitionDate(nextIssuedInvoiceDueDate.Value) : null);
    }

    private static DateOnly GetTransitionDate(DateOnly sourceDate)
    {
        return sourceDate == DateOnly.MaxValue ? DateOnly.MaxValue : sourceDate.AddDays(1);
    }
}
