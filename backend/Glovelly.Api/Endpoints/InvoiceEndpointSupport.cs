using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class InvoiceEndpointSupport
{
    public static async Task<DateOnly?> ResolveInvoicePeriodDateAsync(
        AppDbContext db,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var firstGigDate = await db.InvoiceLines
            .AsNoTracking()
            .Where(line => line.InvoiceId == invoiceId && line.GigId.HasValue)
            .Join(
                db.Gigs.AsNoTracking(),
                line => line.GigId!.Value,
                gig => gig.Id,
                (_, gig) => gig.Date)
            .OrderBy(date => date)
            .FirstOrDefaultAsync(cancellationToken);

        return firstGigDate == default
            ? null
            : new DateOnly(firstGigDate.Year, firstGigDate.Month, 1);
    }

    public static IQueryable<Invoice> WhereContributingToPaidIncome(
        this IQueryable<Invoice> query,
        FinancialYearPeriod period)
    {
        return query.Where(invoice =>
            invoice.Status == InvoiceStatus.Paid &&
            invoice.PaidOn.HasValue &&
            invoice.PaidOn.Value >= period.Start &&
            invoice.PaidOn.Value <= period.End);
    }
}
