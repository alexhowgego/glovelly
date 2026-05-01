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
        var sellerProfile = await ResolveSellerProfileAsync(userId, cancellationToken);
        invoice.PdfBlob = GenerateInvoicePdf(invoice, client, gig, generatedLines, sellerProfile);

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
        return ReissueInvoiceInternalAsync(invoice, client, userId, cancellationToken);
    }

    public Task RedraftInvoiceAsync(
        Invoice invoice,
        Client client,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        return RedraftInvoiceInternalAsync(invoice, client, userId, cancellationToken);
    }

    public async Task IssueInvoiceAsync(
        Invoice invoice,
        Client client,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var issuedUtc = DateTimeOffset.UtcNow;
        var invoiceDate = DateOnly.FromDateTime(issuedUtc.UtcDateTime);
        invoice.InvoiceDate = invoiceDate;
        invoice.DueDate = invoiceDate.AddDays(14);
        invoice.Status = InvoiceStatus.Issued;
        invoice.StatusUpdatedUtc = issuedUtc;

        if (!invoice.FirstIssuedUtc.HasValue)
        {
            invoice.FirstIssuedUtc = issuedUtc;
            invoice.FirstIssuedByUserId = userId;
        }

        var sellerProfile = await ResolveSellerProfileAsync(userId, cancellationToken);
        invoice.PdfBlob = GenerateInvoicePdf(invoice, client, null, invoice.Lines.ToList(), sellerProfile);
        StampUpdate(invoice, userId);
    }

    private async Task ReissueInvoiceInternalAsync(
        Invoice invoice,
        Client client,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var reissuedUtc = DateTimeOffset.UtcNow;
        var invoiceDate = DateOnly.FromDateTime(reissuedUtc.UtcDateTime);
        invoice.InvoiceDate = invoiceDate;
        invoice.DueDate = invoiceDate.AddDays(14);
        var sellerProfile = await ResolveSellerProfileAsync(userId, cancellationToken);
        invoice.PdfBlob = GenerateInvoicePdf(invoice, client, null, invoice.Lines.ToList(), sellerProfile);
        invoice.Status = InvoiceStatus.Draft;
        invoice.StatusUpdatedUtc = reissuedUtc;
        invoice.ReissueCount += 1;
        invoice.LastReissuedUtc = reissuedUtc;
        invoice.LastReissuedByUserId = userId;
        StampUpdate(invoice, userId);
    }

    private async Task RedraftInvoiceInternalAsync(
        Invoice invoice,
        Client client,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var redraftedUtc = DateTimeOffset.UtcNow;
        var invoiceDate = DateOnly.FromDateTime(redraftedUtc.UtcDateTime);
        invoice.InvoiceDate = invoiceDate;
        invoice.DueDate = invoiceDate.AddDays(14);
        var sellerProfile = await ResolveSellerProfileAsync(userId, cancellationToken);
        invoice.PdfBlob = GenerateInvoicePdf(invoice, client, null, invoice.Lines.ToList(), sellerProfile);
        invoice.Status = InvoiceStatus.Draft;
        StampUpdate(invoice, userId);
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

    private Task<SellerProfile?> ResolveSellerProfileAsync(
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
        {
            return Task.FromResult<SellerProfile?>(null);
        }

        return dbContext.SellerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId.Value, cancellationToken);
    }

    private static byte[] GenerateInvoicePdf(
        Invoice invoice,
        Client client,
        Gig? gig,
        IReadOnlyCollection<InvoiceLine> lines,
        SellerProfile? sellerProfile)
    {
        var description = invoice.Description ?? (gig is null ? "In respect of services rendered." : BuildInvoiceDescription(gig));
        var sellerName = string.IsNullOrWhiteSpace(sellerProfile?.SellerName)
            ? "Glovelly"
            : sellerProfile.SellerName!;
        var sellerLines = BuildSellerLines(sellerProfile);
        var billToLines = BuildBillToLines(client);
        var paymentDetails = BuildPaymentDetails(invoice, sellerProfile);
        var orderedLines = lines
            .OrderBy(value => value.SortOrder)
            .Select(line => new InvoicePdfLineItem(
                line.Description,
                line.Quantity.ToString("0.##", CultureInfo.InvariantCulture),
                FormatCurrency(line.UnitPrice),
                FormatCurrency(line.LineTotal)))
            .ToList();

        var document = new PdfDocumentBuilder();
        var renderer = new InvoicePdfRenderer(document);
        renderer.Render(new InvoicePdfViewModel(
            SellerName: sellerName,
            SellerLines: sellerLines,
            InvoiceNumber: invoice.InvoiceNumber,
            InvoiceDate: invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DueDate: invoice.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            BillToLines: billToLines,
            Description: description,
            LineItems: orderedLines,
            TotalDue: FormatCurrency(invoice.Total),
            PaymentDetails: paymentDetails));

        return document.Build();
    }

    private static List<string> BuildSellerLines(SellerProfile? sellerProfile)
    {
        if (sellerProfile is null)
        {
            return [];
        }

        var sellerLines = new List<string>();

        if (!string.IsNullOrWhiteSpace(sellerProfile.Address.Line1))
        {
            sellerLines.Add(sellerProfile.Address.Line1);
        }

        if (!string.IsNullOrWhiteSpace(sellerProfile.Address.Line2))
        {
            sellerLines.Add(sellerProfile.Address.Line2);
        }

        var cityLine = string.Join(", ", new[]
        {
            sellerProfile.Address.City,
            sellerProfile.Address.StateOrCounty,
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(cityLine))
        {
            sellerLines.Add(cityLine);
        }

        if (!string.IsNullOrWhiteSpace(sellerProfile.Address.PostalCode))
        {
            sellerLines.Add(sellerProfile.Address.PostalCode);
        }

        if (!string.IsNullOrWhiteSpace(sellerProfile.Address.Country))
        {
            sellerLines.Add(sellerProfile.Address.Country);
        }

        if (!string.IsNullOrWhiteSpace(sellerProfile.Email))
        {
            sellerLines.Add($"Email: {sellerProfile.Email}");
        }

        if (!string.IsNullOrWhiteSpace(sellerProfile.Phone))
        {
            sellerLines.Add($"Phone: {sellerProfile.Phone}");
        }

        return sellerLines;
    }

    private static List<string> BuildBillToLines(Client client)
    {
        var billToLines = new List<string> { client.Name, client.Email };

        if (!string.IsNullOrWhiteSpace(client.BillingAddress?.Line1))
        {
            billToLines.Add(client.BillingAddress.Line1);
        }

        if (!string.IsNullOrWhiteSpace(client.BillingAddress?.Line2))
        {
            billToLines.Add(client.BillingAddress.Line2);
        }

        var cityLine = string.Join(", ", new[]
        {
            client.BillingAddress?.City,
            client.BillingAddress?.StateOrCounty
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(cityLine))
        {
            billToLines.Add(cityLine);
        }

        if (!string.IsNullOrWhiteSpace(client.BillingAddress?.PostalCode))
        {
            billToLines.Add(client.BillingAddress.PostalCode);
        }

        if (!string.IsNullOrWhiteSpace(client.BillingAddress?.Country))
        {
            billToLines.Add(client.BillingAddress.Country);
        }

        return billToLines;
    }

    private static List<string> BuildPaymentDetails(Invoice invoice, SellerProfile? sellerProfile)
    {
        var paymentDetails = new List<string>
        {
            $"Payment due by {invoice.DueDate:yyyy-MM-dd}.",
            $"Reference: {invoice.InvoiceNumber}.",
        };

        if (sellerProfile is not null)
        {
            if (!string.IsNullOrWhiteSpace(sellerProfile.AccountName))
            {
                paymentDetails.Add($"Account name: {sellerProfile.AccountName}");
            }

            if (!string.IsNullOrWhiteSpace(sellerProfile.SortCode))
            {
                paymentDetails.Add($"Sort code: {sellerProfile.SortCode}");
            }

            if (!string.IsNullOrWhiteSpace(sellerProfile.AccountNumber))
            {
                paymentDetails.Add($"Account number: {sellerProfile.AccountNumber}");
            }

            if (!string.IsNullOrWhiteSpace(sellerProfile.PaymentReferenceNote))
            {
                paymentDetails.Add($"Payment note: {sellerProfile.PaymentReferenceNote}");
            }
        }
        else
        {
            paymentDetails.Add("Please use the agreed payment method and quote the invoice number with payment.");
        }

        return paymentDetails;
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"GBP {amount:0.00}";
    }

    private sealed record InvoicePdfViewModel(
        string SellerName,
        IReadOnlyList<string> SellerLines,
        string InvoiceNumber,
        string InvoiceDate,
        string DueDate,
        IReadOnlyList<string> BillToLines,
        string Description,
        IReadOnlyList<InvoicePdfLineItem> LineItems,
        string TotalDue,
        IReadOnlyList<string> PaymentDetails);

    private sealed record InvoicePdfLineItem(
        string Description,
        string Quantity,
        string Rate,
        string Amount);

    private sealed class InvoicePdfRenderer(PdfDocumentBuilder document)
    {
        private const double PageWidth = 595d;
        private const double PageHeight = 842d;
        private const double Margin = 48d;
        private const double BottomMargin = 52d;
        private const double RowPadding = 8d;
        private const double DescriptionColumnWidth = 274d;
        private const double QuantityColumnWidth = 55d;
        private const double RateColumnWidth = 84d;
        private const double AmountColumnWidth = 86d;

        private readonly PdfDocumentBuilder _document = document;
        private PdfPageCanvas _canvas = document.AddPage();
        private double _cursorY = PageHeight - Margin;

        public void Render(InvoicePdfViewModel model)
        {
            RenderHeader(model);
            RenderBillTo(model);
            RenderDescription(model);
            RenderLineItemsTable(model);
            RenderTotals(model);
            RenderPaymentDetails(model);
        }

        private void RenderHeader(InvoicePdfViewModel model)
        {
            _canvas.DrawText(Margin, _cursorY, model.SellerName, "F2", 16, 0.15);
            _cursorY -= 20;

            foreach (var line in model.SellerLines)
            {
                _canvas.DrawText(Margin, _cursorY, line, "F1", 10, 0.12);
                _cursorY -= 14;
            }

            _cursorY -= 22;
            _canvas.DrawText(Margin, _cursorY, "Invoice", "F2", 28, 0.05);

            const double boxWidth = 188d;
            const double boxHeight = 86d;
            var boxX = PageWidth - Margin - boxWidth;
            var boxY = PageHeight - Margin - boxHeight + 12;

            _canvas.FillRectangle(boxX, boxY, boxWidth, boxHeight, 0.95);
            _canvas.StrokeRectangle(boxX, boxY, boxWidth, boxHeight, 0.82, 0.8);
            DrawMetadataRow(boxX + 14, boxY + 60, "Invoice number", model.InvoiceNumber);
            DrawMetadataRow(boxX + 14, boxY + 40, "Invoice date", model.InvoiceDate);
            DrawMetadataRow(boxX + 14, boxY + 20, "Due date", model.DueDate);

            _cursorY = Math.Min(_cursorY - 14, boxY - 24);
            _canvas.DrawLine(Margin, _cursorY, PageWidth - Margin, _cursorY, 0.82, 0.75);
            _cursorY -= 28;
        }

        private void DrawMetadataRow(double x, double y, string label, string value)
        {
            _canvas.DrawText(x, y, label, "F1", 9, 0.45);
            _canvas.DrawText(x + 78, y, value, "F2", 10, 0.1);
        }

        private void RenderBillTo(InvoicePdfViewModel model)
        {
            var lineCount = Math.Max(model.BillToLines.Count, 1);
            var boxHeight = 30 + (lineCount * 14);
            EnsureSpace(boxHeight + 12, null);

            var boxY = _cursorY - boxHeight;
            _canvas.FillRectangle(Margin, boxY, 250, boxHeight, 0.97);
            _canvas.StrokeRectangle(Margin, boxY, 250, boxHeight, 0.86, 0.75);
            _canvas.DrawText(Margin + 12, _cursorY - 18, "Bill to", "F2", 11, 0.15);

            var textY = _cursorY - 36;
            foreach (var line in model.BillToLines)
            {
                _canvas.DrawText(Margin + 12, textY, line, "F1", 10, 0.12);
                textY -= 14;
            }

            _cursorY = boxY - 22;
        }

        private void RenderDescription(InvoicePdfViewModel model)
        {
            var wrappedDescription = WrapText(model.Description, PageWidth - (Margin * 2) - 24, 10);
            var boxHeight = 34 + (wrappedDescription.Count * 14);
            EnsureSpace(boxHeight + 10, null);

            var boxY = _cursorY - boxHeight;
            _canvas.FillRectangle(Margin, boxY, PageWidth - (Margin * 2), boxHeight, 0.98);
            _canvas.StrokeRectangle(Margin, boxY, PageWidth - (Margin * 2), boxHeight, 0.88, 0.75);
            _canvas.DrawText(Margin + 12, _cursorY - 18, "Description", "F2", 11, 0.15);

            var textY = _cursorY - 36;
            foreach (var line in wrappedDescription)
            {
                _canvas.DrawText(Margin + 12, textY, line, "F1", 10, 0.12);
                textY -= 14;
            }

            _cursorY = boxY - 22;
        }

        private void RenderLineItemsTable(InvoicePdfViewModel model)
        {
            DrawLineItemsHeader();

            foreach (var lineItem in model.LineItems)
            {
                var wrappedDescription = WrapText(lineItem.Description, DescriptionColumnWidth - (RowPadding * 2), 10);
                var contentHeight = Math.Max(20d, wrappedDescription.Count * 13d);
                var rowHeight = contentHeight + (RowPadding * 2);
                EnsureSpace(rowHeight + 2, DrawLineItemsHeader);

                var rowTop = _cursorY;
                var rowBottom = rowTop - rowHeight;
                var rowWidth = DescriptionColumnWidth + QuantityColumnWidth + RateColumnWidth + AmountColumnWidth;

                _canvas.StrokeRectangle(Margin, rowBottom, rowWidth, rowHeight, 0.9, 0.6);
                _canvas.DrawLine(Margin + DescriptionColumnWidth, rowBottom, Margin + DescriptionColumnWidth, rowTop, 0.9, 0.6);
                _canvas.DrawLine(Margin + DescriptionColumnWidth + QuantityColumnWidth, rowBottom, Margin + DescriptionColumnWidth + QuantityColumnWidth, rowTop, 0.9, 0.6);
                _canvas.DrawLine(Margin + DescriptionColumnWidth + QuantityColumnWidth + RateColumnWidth, rowBottom, Margin + DescriptionColumnWidth + QuantityColumnWidth + RateColumnWidth, rowTop, 0.9, 0.6);

                var descriptionY = rowTop - RowPadding - 10;
                foreach (var wrappedLine in wrappedDescription)
                {
                    _canvas.DrawText(Margin + RowPadding, descriptionY, wrappedLine, "F1", 10, 0.1);
                    descriptionY -= 13;
                }

                var centeredY = rowTop - RowPadding - 10;
                _canvas.DrawText(Margin + DescriptionColumnWidth + RowPadding, centeredY, lineItem.Quantity, "F1", 10, 0.1);
                _canvas.DrawText(Margin + DescriptionColumnWidth + QuantityColumnWidth + RowPadding, centeredY, lineItem.Rate, "F1", 10, 0.1);
                _canvas.DrawText(Margin + DescriptionColumnWidth + QuantityColumnWidth + RateColumnWidth + RowPadding, centeredY, lineItem.Amount, "F2", 10, 0.1);

                _cursorY = rowBottom - 2;
            }

            _cursorY -= 18;
        }

        private void DrawLineItemsHeader()
        {
            const double headerHeight = 24d;
            EnsureSpace(headerHeight + 4, null);

            var rowWidth = DescriptionColumnWidth + QuantityColumnWidth + RateColumnWidth + AmountColumnWidth;
            var rowBottom = _cursorY - headerHeight;

            _canvas.FillRectangle(Margin, rowBottom, rowWidth, headerHeight, 0.93);
            _canvas.StrokeRectangle(Margin, rowBottom, rowWidth, headerHeight, 0.82, 0.7);
            _canvas.DrawLine(Margin + DescriptionColumnWidth, rowBottom, Margin + DescriptionColumnWidth, _cursorY, 0.82, 0.7);
            _canvas.DrawLine(Margin + DescriptionColumnWidth + QuantityColumnWidth, rowBottom, Margin + DescriptionColumnWidth + QuantityColumnWidth, _cursorY, 0.82, 0.7);
            _canvas.DrawLine(Margin + DescriptionColumnWidth + QuantityColumnWidth + RateColumnWidth, rowBottom, Margin + DescriptionColumnWidth + QuantityColumnWidth + RateColumnWidth, _cursorY, 0.82, 0.7);
            _canvas.DrawText(Margin + RowPadding, _cursorY - 16, "Description", "F2", 10, 0.12);
            _canvas.DrawText(Margin + DescriptionColumnWidth + RowPadding, _cursorY - 16, "Qty", "F2", 10, 0.12);
            _canvas.DrawText(Margin + DescriptionColumnWidth + QuantityColumnWidth + RowPadding, _cursorY - 16, "Rate", "F2", 10, 0.12);
            _canvas.DrawText(Margin + DescriptionColumnWidth + QuantityColumnWidth + RateColumnWidth + RowPadding, _cursorY - 16, "Amount", "F2", 10, 0.12);

            _cursorY = rowBottom - 2;
        }

        private void RenderTotals(InvoicePdfViewModel model)
        {
            const double boxWidth = 188d;
            const double boxHeight = 44d;
            EnsureSpace(boxHeight + 20, null);

            var boxX = PageWidth - Margin - boxWidth;
            var boxY = _cursorY - boxHeight;
            _canvas.FillRectangle(boxX, boxY, boxWidth, boxHeight, 0.95);
            _canvas.StrokeRectangle(boxX, boxY, boxWidth, boxHeight, 0.82, 0.75);
            _canvas.DrawText(boxX + 14, boxY + 26, "Total due", "F2", 11, 0.15);
            _canvas.DrawText(boxX + 14, boxY + 11, model.TotalDue, "F2", 16, 0.05);

            _cursorY = boxY - 24;
        }

        private void RenderPaymentDetails(InvoicePdfViewModel model)
        {
            var wrappedLines = model.PaymentDetails
                .SelectMany(line => WrapText(line, PageWidth - (Margin * 2) - 24, 10))
                .ToList();
            var boxHeight = 34 + (wrappedLines.Count * 14);
            EnsureSpace(boxHeight, null);

            var boxY = _cursorY - boxHeight;
            _canvas.FillRectangle(Margin, boxY, PageWidth - (Margin * 2), boxHeight, 0.97);
            _canvas.StrokeRectangle(Margin, boxY, PageWidth - (Margin * 2), boxHeight, 0.86, 0.75);
            _canvas.DrawText(Margin + 12, _cursorY - 18, "Payment details", "F2", 11, 0.15);

            var textY = _cursorY - 36;
            foreach (var line in wrappedLines)
            {
                _canvas.DrawText(Margin + 12, textY, line, "F1", 10, 0.12);
                textY -= 14;
            }
        }

        private void EnsureSpace(double requiredHeight, Action? onNewPage)
        {
            if (_cursorY - requiredHeight >= BottomMargin)
            {
                return;
            }

            _canvas = _document.AddPage();
            _cursorY = PageHeight - Margin;
            RenderContinuationHeader();
            onNewPage?.Invoke();
        }

        private void RenderContinuationHeader()
        {
            _canvas.DrawText(Margin, _cursorY, "Invoice continued", "F2", 14, 0.15);
            _cursorY -= 20;
            _canvas.DrawLine(Margin, _cursorY, PageWidth - Margin, _cursorY, 0.88, 0.6);
            _cursorY -= 24;
        }

        private static List<string> WrapText(string value, double maxWidth, double fontSize)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return [string.Empty];
            }

            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (EstimateTextWidth(candidate, fontSize) <= maxWidth)
                {
                    currentLine.Clear();
                    currentLine.Append(candidate);
                    continue;
                }

                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                }

                currentLine.Append(word);
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }

            return lines;
        }

        private static double EstimateTextWidth(string value, double fontSize)
        {
            return value.Length * fontSize * 0.48;
        }
    }

    private sealed class PdfDocumentBuilder
    {
        private readonly List<string> _pageContents = [];

        public PdfPageCanvas AddPage()
        {
            _pageContents.Add(string.Empty);
            var pageIndex = _pageContents.Count - 1;
            return new PdfPageCanvas(value => _pageContents[pageIndex] += value);
        }

        public byte[] Build()
        {
            var pageCount = _pageContents.Count;
            var objects = new List<string>
            {
                "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj",
                $"2 0 obj << /Type /Pages /Count {pageCount} /Kids [{string.Join(' ', Enumerable.Range(0, pageCount).Select(index => $"{5 + (index * 2)} 0 R"))}] >> endobj",
                "3 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj",
                "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >> endobj"
            };

            for (var index = 0; index < pageCount; index++)
            {
                var pageObjectId = 5 + (index * 2);
                var contentObjectId = pageObjectId + 1;
                var content = _pageContents[index];

                objects.Add(
                    $"{pageObjectId} 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObjectId} 0 R >> endobj");
                objects.Add(
                    $"{contentObjectId} 0 obj << /Length {Encoding.ASCII.GetByteCount(content)} >> stream\n{content}endstream\nendobj");
            }

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
            pdfBuilder.Append($"xref\n0 {objects.Count + 1}\n");
            pdfBuilder.Append("0000000000 65535 f \n");

            foreach (var offset in offsets.Skip(1))
            {
                pdfBuilder.Append($"{offset:D10} 00000 n \n");
            }

            pdfBuilder.Append("trailer\n");
            pdfBuilder.Append($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
            pdfBuilder.Append("startxref\n");
            pdfBuilder.Append($"{xrefOffset}\n");
            pdfBuilder.Append("%%EOF");

            return Encoding.ASCII.GetBytes(pdfBuilder.ToString());
        }
    }

    private sealed class PdfPageCanvas(Action<string> append)
    {
        private readonly Action<string> _append = append;

        public void DrawText(double x, double y, string text, string fontName, double fontSize, double gray)
        {
            _append($"{Format(gray)} g BT /{fontName} {Format(fontSize)} Tf {Format(x)} {Format(y)} Td ({EscapePdfText(text)}) Tj ET\n");
        }

        public void DrawLine(double x1, double y1, double x2, double y2, double gray, double width)
        {
            _append($"{Format(gray)} G {Format(width)} w {Format(x1)} {Format(y1)} m {Format(x2)} {Format(y2)} l S\n");
        }

        public void FillRectangle(double x, double y, double width, double height, double gray)
        {
            _append($"{Format(gray)} g {Format(x)} {Format(y)} {Format(width)} {Format(height)} re f\n");
        }

        public void StrokeRectangle(double x, double y, double width, double height, double gray, double strokeWidth)
        {
            _append($"{Format(gray)} G {Format(strokeWidth)} w {Format(x)} {Format(y)} {Format(width)} {Format(height)} re S\n");
        }

        private static string Format(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
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
