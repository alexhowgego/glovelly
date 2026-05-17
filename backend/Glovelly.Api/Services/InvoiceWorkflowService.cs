using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class InvoiceWorkflowService(
    AppDbContext dbContext,
    IInvoiceNumberService invoiceNumberService,
    IInvoiceLineGenerationService invoiceLineGenerationService,
    IInvoiceProfileDefaultsService invoiceProfileDefaultsService,
    IInvoicePdfRenderer invoicePdfRenderer,
    IInvoicePdfService invoicePdfService) : IInvoiceWorkflowService
{
    public async Task<GenerateInvoiceFromGigSelectionResult> GenerateInvoiceFromGigSelectionAsync(
        IReadOnlyCollection<Guid> gigIds,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var selectedGigIds = gigIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (selectedGigIds.Count == 0)
        {
            return ValidationFailed("gigIds", "Select at least one gig.");
        }

        var gigs = await dbContext.Gigs
            .Where(gig => gig.CreatedByUserId == null || gig.CreatedByUserId == userId)
            .Include(gig => gig.Client)
            .Include(gig => gig.Expenses)
            .Where(gig => selectedGigIds.Contains(gig.Id))
            .OrderBy(gig => gig.Date)
            .ThenBy(gig => gig.Title)
            .ToListAsync(cancellationToken);

        if (gigs.Count != selectedGigIds.Count)
        {
            return ValidationFailed("gigIds", "One or more selected gigs do not exist.");
        }

        if (gigs.Any(gig => gig.InvoiceId.HasValue))
        {
            return new GenerateInvoiceFromGigSelectionResult(
                GenerateInvoiceFromGigSelectionStatus.Conflict,
                ConflictMessage: "All selected gigs must be uninvoiced before creating a combined invoice.");
        }

        var distinctClientIds = gigs
            .Select(gig => gig.ClientId)
            .Distinct()
            .ToList();

        if (distinctClientIds.Count != 1)
        {
            return ValidationFailed("gigIds", "Selected gigs must all belong to the same client.");
        }

        var client = gigs[0].Client;
        if (client is null)
        {
            return ValidationFailed("clientId", "Client does not exist.");
        }

        var firstGig = gigs[0];
        var invoice = await GenerateInvoiceForGigAsync(firstGig, client, userId, cancellationToken);

        foreach (var gig in gigs.Skip(1))
        {
            gig.InvoiceId = invoice.Id;
            gig.InvoicedAt = DateTimeOffset.UtcNow;
            StampUpdate(gig, userId);
            await SyncGeneratedInvoiceLinesForGigAsync(gig, userId, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var invoiceId = invoice.Id;
        var refreshedInvoice = await dbContext.Invoices
            .Where(invoice => invoice.CreatedByUserId == null || invoice.CreatedByUserId == userId)
            .Include(invoice => invoice.Lines)
            .FirstAsync(value => value.Id == invoiceId, cancellationToken);

        await RedraftInvoiceAsync(refreshedInvoice, client, userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new GenerateInvoiceFromGigSelectionResult(
            GenerateInvoiceFromGigSelectionStatus.Created,
            Invoice: refreshedInvoice);
    }

    public async Task<Invoice> GenerateInvoiceForGigAsync(
        Gig gig,
        Client client,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var paymentWindowDays = await invoiceProfileDefaultsService.ResolvePaymentWindowDaysAsync(userId, cancellationToken);
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = await invoiceNumberService.GenerateInvoiceNumberAsync(invoiceDate, cancellationToken),
            ClientId = gig.ClientId,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(paymentWindowDays),
            Status = InvoiceStatus.Draft,
            Description = InvoiceDescriptionBuilder.ForGig(gig),
            Client = null,
        };

        StampCreate(invoice, userId);

        gig.InvoiceId = invoice.Id;
        gig.InvoicedAt = DateTimeOffset.UtcNow;
        gig.Invoice = invoice;
        StampUpdate(gig, userId);

        var generatedLines = await invoiceLineGenerationService.BuildGeneratedInvoiceLinesForGigAsync(gig, userId, cancellationToken);
        invoice.Lines = generatedLines;
        var sellerProfile = await invoiceProfileDefaultsService.ResolveSellerProfileAsync(userId, cancellationToken);
        await invoicePdfService.SaveGeneratedPdfAsync(
            invoice,
            userId,
            invoicePdfRenderer.RenderInvoicePdf(invoice, client, gig, generatedLines, sellerProfile),
            cancellationToken);

        dbContext.Invoices.Add(invoice);

        return invoice;
    }

    private static GenerateInvoiceFromGigSelectionResult ValidationFailed(
        string field,
        string message)
    {
        return new GenerateInvoiceFromGigSelectionResult(
            GenerateInvoiceFromGigSelectionStatus.ValidationFailed,
            ValidationErrors: new Dictionary<string, string[]>
            {
                [field] = [message]
            });
    }

    public async Task SyncGeneratedInvoiceLinesForGigAsync(
        Gig gig,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        _ = await invoiceLineGenerationService.RemoveSystemGeneratedInvoiceLinesForGigAsync(gig.Id, cancellationToken);

        if (!gig.InvoiceId.HasValue)
        {
            return;
        }

        var lines = await invoiceLineGenerationService.BuildGeneratedInvoiceLinesForGigAsync(gig, userId, cancellationToken);
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
        return invoiceLineGenerationService.RemoveSystemGeneratedInvoiceLinesForGigAsync(gigId, cancellationToken);
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
        var paymentWindowDays = await invoiceProfileDefaultsService.ResolvePaymentWindowDaysAsync(userId, cancellationToken);
        invoice.InvoiceDate = invoiceDate;
        invoice.DueDate = invoiceDate.AddDays(paymentWindowDays);
        invoice.Status = InvoiceStatus.Issued;
        invoice.StatusUpdatedUtc = issuedUtc;

        if (!invoice.FirstIssuedUtc.HasValue)
        {
            invoice.FirstIssuedUtc = issuedUtc;
            invoice.FirstIssuedByUserId = userId;
        }

        var sellerProfile = await invoiceProfileDefaultsService.ResolveSellerProfileAsync(userId, cancellationToken);
        await invoicePdfService.SaveGeneratedPdfAsync(
            invoice,
            userId,
            invoicePdfRenderer.RenderInvoicePdf(invoice, client, null, invoice.Lines.ToList(), sellerProfile),
            cancellationToken);
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
        var paymentWindowDays = await invoiceProfileDefaultsService.ResolvePaymentWindowDaysAsync(userId, cancellationToken);
        invoice.InvoiceDate = invoiceDate;
        invoice.DueDate = invoiceDate.AddDays(paymentWindowDays);
        var sellerProfile = await invoiceProfileDefaultsService.ResolveSellerProfileAsync(userId, cancellationToken);
        await invoicePdfService.SaveGeneratedPdfAsync(
            invoice,
            userId,
            invoicePdfRenderer.RenderInvoicePdf(invoice, client, null, invoice.Lines.ToList(), sellerProfile),
            cancellationToken);
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
        var paymentWindowDays = await invoiceProfileDefaultsService.ResolvePaymentWindowDaysAsync(userId, cancellationToken);
        invoice.InvoiceDate = invoiceDate;
        invoice.DueDate = invoiceDate.AddDays(paymentWindowDays);
        var sellerProfile = await invoiceProfileDefaultsService.ResolveSellerProfileAsync(userId, cancellationToken);
        await invoicePdfService.SaveGeneratedPdfAsync(
            invoice,
            userId,
            invoicePdfRenderer.RenderInvoicePdf(invoice, client, null, invoice.Lines.ToList(), sellerProfile),
            cancellationToken);
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
        var adjustmentLine = await invoiceLineGenerationService.CreateManualAdjustmentAsync(
            invoice,
            amount,
            reason,
            userId,
            cancellationToken);
        StampUpdate(invoice, userId);

        return adjustmentLine;
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

}
