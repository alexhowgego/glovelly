using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Glovelly.Api.Services;

public sealed class InvoiceWorkflowService(AppDbContext dbContext) : IInvoiceWorkflowService
{
    public async Task<Invoice> GenerateInvoiceForGigAsync(
        Gig gig,
        Client client,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = await GenerateInvoiceNumberAsync(invoiceDate, cancellationToken),
            ClientId = gig.ClientId,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(14),
            Status = InvoiceStatus.Draft,
            Description = BuildInvoiceDescription(gig),
            Client = null,
        };

        StampCreate(invoice, userId);

        gig.InvoiceId = invoice.Id;
        gig.InvoicedAt = DateTimeOffset.UtcNow;
        gig.Invoice = invoice;
        StampUpdate(gig, userId);

        var generatedLines = await BuildGeneratedInvoiceLinesForGigAsync(gig, userId, cancellationToken);
        invoice.Lines = generatedLines;
        invoice.PdfBlob = GenerateInvoicePdf(invoice, client, gig, generatedLines);

        dbContext.Invoices.Add(invoice);

        return invoice;
    }

    public async Task SyncGeneratedInvoiceLinesForGigAsync(
        Gig gig,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        _ = await RemoveSystemGeneratedInvoiceLinesForGigInternalAsync(gig.Id, cancellationToken);

        if (!gig.InvoiceId.HasValue)
        {
            return;
        }

        var lines = await BuildGeneratedInvoiceLinesForGigAsync(gig, userId, cancellationToken);
        if (lines.Count == 0)
        {
            return;
        }

        dbContext.InvoiceLines.AddRange(lines);
    }

    public Task<bool> RemoveSystemGeneratedInvoiceLinesForGigAsync(
        Guid gigId,
        CancellationToken cancellationToken = default)
    {
        return RemoveSystemGeneratedInvoiceLinesForGigInternalAsync(gigId, cancellationToken);
    }

    public Task ReissueInvoiceAsync(
        Invoice invoice,
        Client client,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        invoice.PdfBlob = GenerateInvoicePdf(invoice, client, null, invoice.Lines.ToList());
        invoice.Status = InvoiceStatus.Draft;
        invoice.StatusUpdatedUtc = DateTimeOffset.UtcNow;
        invoice.ReissueCount += 1;
        invoice.LastReissuedUtc = DateTimeOffset.UtcNow;
        invoice.LastReissuedByUserId = userId;
        StampUpdate(invoice, userId);

        return Task.CompletedTask;
    }

    public async Task<InvoiceLine> CreateManualAdjustmentAsync(
        Invoice invoice,
        decimal amount,
        string reason,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var adjustmentLine = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            SortOrder = await GetNextSortOrderAsync(invoice.Id, cancellationToken),
            Type = InvoiceLineType.ManualAdjustment,
            Description = $"Manual adjustment: {reason}",
            Quantity = 1m,
            UnitPrice = amount,
            CalculationNotes = BuildAdjustmentAuditNote(userId),
            IsSystemGenerated = false,
        };
        StampCreate(adjustmentLine, userId);

        dbContext.InvoiceLines.Add(adjustmentLine);
        StampUpdate(invoice, userId);

        return adjustmentLine;
    }

    private async Task<bool> RemoveSystemGeneratedInvoiceLinesForGigInternalAsync(
        Guid gigId,
        CancellationToken cancellationToken)
    {
        var generatedLines = await dbContext.InvoiceLines
            .Where(line => line.GigId == gigId && line.IsSystemGenerated)
            .ToListAsync(cancellationToken);

        if (generatedLines.Count == 0)
        {
            return false;
        }

        dbContext.InvoiceLines.RemoveRange(generatedLines);
        return true;
    }

    private async Task<List<InvoiceLine>> BuildGeneratedInvoiceLinesForGigAsync(
        Gig gig,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var lines = new List<InvoiceLine>();
        var nextSortOrder = await GetNextSortOrderAsync(gig.InvoiceId!.Value, cancellationToken);
        var (mileageRate, passengerMileageRate) =
            await ResolveMileageRatesAsync(gig.ClientId, userId, cancellationToken);

        if (gig.Fee != 0)
        {
            lines.Add(CreateGeneratedLine(
                gig,
                nextSortOrder++,
                InvoiceLineType.PerformanceFee,
                $"Performance fee for {gig.Title} ({gig.Date:yyyy-MM-dd})",
                1m,
                gig.Fee));
        }

        if (gig.TravelMiles > 0 && mileageRate.HasValue && mileageRate.Value != 0)
        {
            lines.Add(CreateGeneratedLine(
                gig,
                nextSortOrder++,
                InvoiceLineType.Mileage,
                $"Mileage for {gig.Title}",
                gig.TravelMiles,
                mileageRate.Value,
                $"{gig.TravelMiles:0.##} miles at {mileageRate.Value:0.##} per mile."));
        }

        var passengerCount = gig.PassengerCount.GetValueOrDefault();

        if (gig.TravelMiles > 0 &&
            passengerCount > 0 &&
            passengerMileageRate.HasValue &&
            passengerMileageRate.Value != 0)
        {
            var passengerMiles = gig.TravelMiles * passengerCount;
            var passengerLabel = passengerCount == 1 ? "passenger" : "passengers";

            lines.Add(CreateGeneratedLine(
                gig,
                nextSortOrder++,
                InvoiceLineType.PassengerMileage,
                $"Passenger mileage for {gig.Title}",
                passengerMiles,
                passengerMileageRate.Value,
                $"{passengerCount} {passengerLabel} x {gig.TravelMiles:0.##} miles."));
        }

        foreach (var expense in gig.Expenses
                     .Where(expense => expense.Amount != 0)
                     .OrderBy(expense => expense.SortOrder)
                     .ThenBy(expense => expense.Description))
        {
            lines.Add(CreateGeneratedLine(
                gig,
                nextSortOrder++,
                InvoiceLineType.MiscExpense,
                expense.Description,
                1m,
                expense.Amount));
        }

        return lines;
    }

    private static InvoiceLine CreateGeneratedLine(
        Gig gig,
        int sortOrder,
        InvoiceLineType type,
        string description,
        decimal quantity,
        decimal unitPrice,
        string? calculationNotes = null)
    {
        return new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = gig.InvoiceId!.Value,
            SortOrder = sortOrder,
            Type = type,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            GigId = gig.Id,
            CalculationNotes = calculationNotes,
            IsSystemGenerated = true,
        };
    }

    private async Task<int> GetNextSortOrderAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var currentMax = await dbContext.InvoiceLines
            .Where(line => line.InvoiceId == invoiceId)
            .Select(line => (int?)line.SortOrder)
            .MaxAsync(cancellationToken);

        return (currentMax ?? 0) + 1;
    }

    private async Task<(decimal? MileageRate, decimal? PassengerMileageRate)> ResolveMileageRatesAsync(
        Guid clientId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var clientRates = await dbContext.Clients
            .AsNoTracking()
            .Where(client => client.Id == clientId)
            .Select(client => new
            {
                client.MileageRate,
                client.PassengerMileageRate,
            })
            .FirstAsync(cancellationToken);

        if (clientRates.MileageRate.HasValue || clientRates.PassengerMileageRate.HasValue || !userId.HasValue)
        {
            return (clientRates.MileageRate, clientRates.PassengerMileageRate);
        }

        var userRates = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId.Value)
            .Select(user => new
            {
                user.MileageRate,
                user.PassengerMileageRate,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return (userRates?.MileageRate, userRates?.PassengerMileageRate);
    }

    private static string BuildInvoiceDescription(Gig gig)
    {
        return $"In respect of {gig.Title} at {gig.Venue} on {gig.Date:yyyy-MM-dd}.";
    }

    private async Task<string> GenerateInvoiceNumberAsync(DateOnly invoiceDate, CancellationToken cancellationToken)
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

    private static byte[] GenerateInvoicePdf(
        Invoice invoice,
        Client client,
        Gig? gig,
        IReadOnlyCollection<InvoiceLine> lines)
    {
        var rows = new List<string>
        {
            "Glovelly Invoice",
            $"Invoice number: {invoice.InvoiceNumber}",
            $"Invoice date: {invoice.InvoiceDate:yyyy-MM-dd}",
            $"Due date: {invoice.DueDate:yyyy-MM-dd}",
            string.Empty,
            $"Bill to: {client.Name}",
            client.Email,
        };

        if (!string.IsNullOrWhiteSpace(client.BillingAddress?.Line1))
        {
            rows.Add(client.BillingAddress.Line1);
        }

        if (!string.IsNullOrWhiteSpace(client.BillingAddress?.Line2))
        {
            rows.Add(client.BillingAddress.Line2);
        }

        var cityLine = string.Join(", ", new[]
        {
            client.BillingAddress?.City,
            client.BillingAddress?.StateOrCounty
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(cityLine))
        {
            rows.Add(cityLine);
        }

        if (!string.IsNullOrWhiteSpace(client.BillingAddress?.PostalCode))
        {
            rows.Add(client.BillingAddress.PostalCode);
        }

        if (!string.IsNullOrWhiteSpace(client.BillingAddress?.Country))
        {
            rows.Add(client.BillingAddress.Country);
        }

        rows.Add(string.Empty);
        rows.Add(invoice.Description ?? (gig is null ? "In respect of services rendered." : BuildInvoiceDescription(gig)));
        rows.Add(string.Empty);
        rows.Add("Line items:");

        foreach (var line in lines.OrderBy(value => value.SortOrder))
        {
            rows.Add(
                $"{line.Description} | Qty {line.Quantity:0.##} x {line.UnitPrice:0.00} = {line.LineTotal:0.00}");
        }

        rows.Add(string.Empty);
        rows.Add($"Total due: {invoice.Total:0.00} GBP");

        return BuildSimplePdf(rows);
    }

    private static byte[] BuildSimplePdf(IEnumerable<string> lines)
    {
        var contentBuilder = new StringBuilder();
        var yPosition = 780;

        foreach (var line in lines)
        {
            contentBuilder.AppendLine($"BT /F1 11 Tf 50 {yPosition} Td ({EscapePdfText(line)}) Tj ET");
            yPosition -= 16;
        }

        var objects = new[]
        {
            "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj",
            "2 0 obj << /Type /Pages /Count 1 /Kids [3 0 R] >> endobj",
            "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj",
            "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj",
            $"5 0 obj << /Length {Encoding.ASCII.GetByteCount(contentBuilder.ToString())} >> stream\n{contentBuilder}endstream\nendobj",
        };

        var pdfBuilder = new StringBuilder();
        pdfBuilder.Append("%PDF-1.4\n");

        var offsets = new List<int> { 0 };
        foreach (var pdfObject in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdfBuilder.ToString()));
            pdfBuilder.Append(pdfObject);
            pdfBuilder.Append('\n');
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdfBuilder.ToString());
        pdfBuilder.Append($"xref\n0 {objects.Length + 1}\n");
        pdfBuilder.Append("0000000000 65535 f \n");

        foreach (var offset in offsets.Skip(1))
        {
            pdfBuilder.Append($"{offset:D10} 00000 n \n");
        }

        pdfBuilder.Append("trailer\n");
        pdfBuilder.Append($"<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        pdfBuilder.Append("startxref\n");
        pdfBuilder.Append($"{xrefOffset}\n");
        pdfBuilder.Append("%%EOF");

        return Encoding.ASCII.GetBytes(pdfBuilder.ToString());
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static void StampCreate(Invoice invoice, Guid? userId)
    {
        invoice.CreatedByUserId = userId;
        invoice.UpdatedByUserId = userId;
    }

    private static void StampUpdate(Invoice invoice, Guid? userId)
    {
        invoice.UpdatedByUserId = userId;
    }

    private static void StampUpdate(Gig gig, Guid? userId)
    {
        gig.UpdatedByUserId = userId;
    }

    private static void StampCreate(InvoiceLine line, Guid? userId)
    {
        line.CreatedByUserId = userId;
        line.CreatedUtc = DateTimeOffset.UtcNow;
    }

    private static string BuildAdjustmentAuditNote(Guid? userId)
    {
        var actor = userId?.ToString() ?? "unknown-user";
        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        return $"Created by {actor} at {timestamp}";
    }
}
