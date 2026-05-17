using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class InvoiceNumberService(AppDbContext dbContext) : IInvoiceNumberService
{
    public async Task<string> GenerateInvoiceNumberAsync(
        DateOnly invoiceDate,
        CancellationToken cancellationToken = default)
    {
        var yearPrefix = $"GLV-{invoiceDate.Year}-";
        var existingNumbers = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.InvoiceNumber.StartsWith(yearPrefix))
            .Select(invoice => invoice.InvoiceNumber)
            .ToListAsync(cancellationToken);

        var nextSequence = existingNumbers
            .Select(value =>
            {
                var suffix = value[yearPrefix.Length..];
                return int.TryParse(suffix, out var parsed) ? parsed : 0;
            })
            .DefaultIfEmpty()
            .Max() + 1;

        return $"{yearPrefix}{nextSequence:000}";
    }
}
