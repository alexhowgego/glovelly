using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class InvoiceDeliveryEndpoints
{
    public static RouteGroupBuilder MapInvoiceDeliveryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/send-email", async (
            Guid id,
            InvoiceEmailDeliveryRequest? request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceDeliveryService invoiceDeliveryService,
            IInvoicePdfService invoicePdfService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("InvoiceEndpoints");
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .Include(value => value.Client)
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id, cancellationToken);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            if (invoice.Client is null)
            {
                return EndpointSupport.ValidationProblem("clientId", "Client does not exist.");
            }

            if (string.IsNullOrWhiteSpace(invoice.Client.Email))
            {
                return EndpointSupport.ValidationProblem("recipient", "Invoice recipient email is missing.");
            }

            var invoicePdf = await invoicePdfService.OpenReadAsync(invoice, cancellationToken);
            if (invoicePdf is null)
            {
                return EndpointSupport.ValidationProblem("pdf", "Invoice PDF is missing.");
            }
            await invoicePdf.Content.DisposeAsync();

            var userDefaultFilenamePattern = userId.HasValue
                ? await db.Users
                    .AsNoTracking()
                    .Where(value => value.Id == userId.Value && value.IsActive)
                    .Select(value => value.InvoiceFilenamePattern)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var periodDate = await InvoiceEndpointSupport.ResolveInvoicePeriodDateAsync(db, invoice.Id, cancellationToken);
            var attachmentFileName = InvoicePdfFilenameBuilder.Build(
                invoice,
                invoice.Client,
                userDefaultFilenamePattern,
                periodDate);
            var sendingUser = userId.HasValue
                ? await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        value => value.Id == userId.Value && value.IsActive,
                        cancellationToken)
                : null;
            var emailSubject = InvoiceEmailSubjectBuilder.Build(
                invoice,
                invoice.Client,
                sendingUser?.InvoiceEmailSubjectPattern,
                periodDate);
            var senderIdentity = InvoiceEmailSenderIdentityBuilder.Build(sendingUser);
            IReadOnlyList<InvoiceExpenseReceiptAttachment> receiptAttachments = request?.IncludeReceipts is true
                ? await BuildInvoiceReceiptAttachmentsAsync(db, invoice, cancellationToken)
                : [];

            try
            {
                await invoiceDeliveryService.DeliverAsync(
                    InvoiceDeliveryChannel.Email,
                    invoice,
                    invoice.Client,
                    userId,
                    request?.Message,
                    emailSubject,
                    attachmentFileName,
                    senderIdentity,
                    cancellationToken,
                    receiptAttachments);
            }
            catch (InvoiceEmailAttachmentLimitExceededException exception)
            {
                return EndpointSupport.ValidationProblem(
                    "attachments",
                    $"Invoice email attachments total {FormatBytes(exception.TotalAttachmentBytes)}, exceeding the configured {FormatBytes(exception.MaxTotalAttachmentBytes)} limit.");
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to send invoice {InvoiceId} ({InvoiceNumber}) by email.",
                    invoice.Id,
                    invoice.InvoiceNumber);
                return Results.Problem(
                    title: "Unable to send invoice email",
                    detail: "We couldn't send the invoice email right now. Please try again shortly.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            EndpointSupport.StampUpdate(invoice, userId);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Invoice {InvoiceId} ({InvoiceNumber}) delivered by {Channel} to {Recipient} by user {UserId}.",
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.LastDeliveryChannel,
                invoice.LastDeliveryRecipient,
                userId);

            return Results.Ok(invoice);
        });

        group.MapPost("/{id:guid}/publish/google-drive", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceDeliveryService invoiceDeliveryService,
            IInvoicePdfService invoicePdfService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("InvoiceEndpoints");
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .Include(value => value.Client)
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id, cancellationToken);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            if (invoice.Client is null)
            {
                return EndpointSupport.ValidationProblem("clientId", "Client does not exist.");
            }

            var invoicePdf = await invoicePdfService.OpenReadAsync(invoice, cancellationToken);
            if (invoicePdf is null)
            {
                return EndpointSupport.ValidationProblem("pdf", "Invoice PDF is missing.");
            }
            await invoicePdf.Content.DisposeAsync();

            var userDefaultFilenamePattern = userId.HasValue
                ? await db.Users
                    .AsNoTracking()
                    .Where(value => value.Id == userId.Value && value.IsActive)
                    .Select(value => value.InvoiceFilenamePattern)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var periodDate = await InvoiceEndpointSupport.ResolveInvoicePeriodDateAsync(db, invoice.Id, cancellationToken);
            var attachmentFileName = InvoicePdfFilenameBuilder.Build(
                invoice,
                invoice.Client,
                userDefaultFilenamePattern,
                periodDate);
            var emailSubject = InvoiceEmailSubjectBuilder.Build(
                invoice,
                invoice.Client,
                defaultPattern: null,
                periodDate);

            InvoiceDeliveryResult deliveryResult;
            try
            {
                deliveryResult = await invoiceDeliveryService.DeliverAsync(
                    InvoiceDeliveryChannel.GoogleDrive,
                    invoice,
                    invoice.Client,
                    userId,
                    null,
                    emailSubject,
                    attachmentFileName,
                    InvoiceEmailSenderIdentityBuilder.Build(null),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to publish invoice {InvoiceId} ({InvoiceNumber}) to Google Drive.",
                    invoice.Id,
                    invoice.InvoiceNumber);
                return Results.Problem(
                    title: "Unable to publish invoice to Google Drive",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            EndpointSupport.StampUpdate(invoice, userId);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Invoice {InvoiceId} ({InvoiceNumber}) delivered by {Channel} to {Recipient} by user {UserId}.",
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.LastDeliveryChannel,
                invoice.LastDeliveryRecipient,
                userId);

            return Results.Ok(new InvoiceGoogleDrivePublishResponse(
                invoice,
                deliveryResult.FileId,
                deliveryResult.FileName,
                deliveryResult.WebViewLink));
        });

        return group;
    }

    private static async Task<IReadOnlyList<InvoiceExpenseReceiptAttachment>> BuildInvoiceReceiptAttachmentsAsync(
        AppDbContext db,
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var expenseLineKeys = invoice.Lines
            .Where(line => line.Type is InvoiceLineType.MiscExpense && line.GigId.HasValue)
            .Select(line => new
            {
                GigId = line.GigId!.Value,
                Description = line.Description.Trim(),
                Amount = line.UnitPrice,
            })
            .ToList();

        if (expenseLineKeys.Count == 0)
        {
            return [];
        }

        var gigIds = expenseLineKeys
            .Select(line => line.GigId)
            .Distinct()
            .ToList();
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

    private static string FormatBytes(long byteCount)
    {
        const decimal oneMegabyte = 1024m * 1024m;
        return byteCount < oneMegabyte
            ? $"{byteCount} bytes"
            : $"{byteCount / oneMegabyte:0.##} MB";
    }

    private sealed record InvoiceEmailDeliveryRequest(string? Message, bool IncludeReceipts = false);

    private sealed record InvoiceGoogleDrivePublishResponse(
        Invoice Invoice,
        string? FileId,
        string? FileName,
        string? WebViewLink);
}
