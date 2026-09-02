using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class InvoiceEmailPreparationService(
    AppDbContext db,
    IInvoiceProfileDefaultsService invoiceProfileDefaultsService,
    IInvoicePdfService invoicePdfService) : IInvoiceEmailPreparationService
{
    public async Task<InvoiceEmailPreparation> PrepareAsync(
        Invoice invoice,
        Guid? userId,
        string? message,
        bool includeReceipts,
        CancellationToken cancellationToken = default)
    {
        var client = invoice.Client
            ?? throw new InvoiceEmailPreparationException("clientId", "Client does not exist.");
        if (string.IsNullOrWhiteSpace(client.Email))
        {
            throw new InvoiceEmailPreparationException("recipient", "Invoice recipient email is missing.");
        }

        var pdfResult = await invoicePdfService.OpenCurrentReadAsync(invoice, cancellationToken);
        if (!pdfResult.IsAvailable)
        {
            throw new InvoiceEmailPreparationException("pdf", pdfResult.UnavailableMessage!);
        }

        await using var pdfContent = pdfResult.Pdf!.Content;
        var userDefaultFilenamePattern = userId.HasValue
            ? await db.Users
                .AsNoTracking()
                .Where(value => value.Id == userId.Value && value.IsActive)
                .Select(value => value.InvoiceFilenamePattern)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var sendingUser = userId.HasValue
            ? await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == userId.Value && value.IsActive, cancellationToken)
            : null;
        var periodDate = await InvoiceEndpointSupport.ResolveInvoicePeriodDateAsync(db, invoice.Id, cancellationToken);
        var attachmentFileName = InvoicePdfFilenameBuilder.Build(
            invoice,
            client,
            userDefaultFilenamePattern,
            periodDate);
        var sellerProfile = await invoiceProfileDefaultsService.ResolveSellerProfileAsync(userId, cancellationToken);
        var businessName = sellerProfile?.SellerName ?? sendingUser?.DisplayName;
        var receiptAttachments = includeReceipts
            ? await BuildReceiptAttachmentsAsync(invoice, cancellationToken)
            : [];
        var renderedEmail = InvoiceEmailTemplateRenderer.Render(
            invoice,
            client,
            sendingUser?.InvoiceEmailBodyTemplate,
            businessName,
            message,
            receiptAttachments.Count > 0);

        return new InvoiceEmailPreparation(
            new InvoiceDeliveryRequest(
                invoice,
                client,
                userId,
                message,
                InvoiceEmailSubjectBuilder.Build(invoice, client, sendingUser?.InvoiceEmailSubjectPattern, periodDate),
                sendingUser?.InvoiceEmailBodyTemplate,
                businessName,
                attachmentFileName,
                InvoiceEmailSenderIdentityBuilder.Build(sendingUser),
                receiptAttachments),
            renderedEmail,
            pdfResult.Pdf.SizeBytes);
    }

    private async Task<IReadOnlyList<InvoiceExpenseReceiptAttachment>> BuildReceiptAttachmentsAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var expenseLineKeys = invoice.Lines
            .Where(line => line.Type is InvoiceLineType.MiscExpense && line.GigId.HasValue)
            .Select(line => new { GigId = line.GigId!.Value, Description = line.Description.Trim(), Amount = line.UnitPrice })
            .ToList();
        if (expenseLineKeys.Count == 0)
        {
            return [];
        }

        var gigIds = expenseLineKeys.Select(line => line.GigId).Distinct().ToList();
        var expenses = await db.GigExpenses
            .AsNoTracking()
            .Include(expense => expense.Attachments)
            .Where(expense => gigIds.Contains(expense.GigId))
            .OrderBy(expense => expense.SortOrder)
            .ThenBy(expense => expense.Description)
            .ToListAsync(cancellationToken);

        return expenses
            .Where(expense => expense.Attachments.Count > 0)
            .Where(expense => expenseLineKeys.Any(line =>
                line.GigId == expense.GigId &&
                string.Equals(line.Description, expense.Description.Trim(), StringComparison.Ordinal) &&
                line.Amount == expense.Amount))
            .SelectMany(expense => expense.Attachments
                .OrderBy(attachment => attachment.CreatedAt)
                .Select(attachment => new InvoiceExpenseReceiptAttachment(
                    expense.Description,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.SizeBytes,
                    attachment.StorageKey)))
            .ToList();
    }
}
