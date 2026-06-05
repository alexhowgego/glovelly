using Glovelly.Api.Data;
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
}
