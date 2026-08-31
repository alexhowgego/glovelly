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
        group.MapPost("/{id:guid}/email-review", async (
            Guid id,
            InvoiceEmailDeliveryRequest? request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceEmailPreparationService invoiceEmailPreparationService,
            IInvoiceReceiptArchiveService invoiceReceiptArchiveService,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await LoadVisibleInvoiceAsync(db, id, userId, cancellationToken);
            if (invoice is null)
            {
                return Results.NotFound();
            }

            try
            {
                var preparation = await invoiceEmailPreparationService.PrepareAsync(
                    invoice, userId, null, includeReceipts: true, cancellationToken: cancellationToken);
                var deliveryRequest = preparation.DeliveryRequest;
                var baseEmail = InvoiceEmailTemplateRenderer.Render(
                    invoice,
                    deliveryRequest.Client,
                    deliveryRequest.EmailBodyTemplate,
                    deliveryRequest.BusinessName);
                return Results.Ok(new InvoiceEmailReviewResponse(
                    deliveryRequest.Client.Name,
                    deliveryRequest.Client.Email!.Trim(),
                    deliveryRequest.EmailSubject,
                    baseEmail.PlainTextBody,
                    deliveryRequest.AttachmentFileName,
                    preparation.PdfSizeBytes,
                    deliveryRequest.ExpenseReceiptAttachments.Count,
                    deliveryRequest.ExpenseReceiptAttachments.Count > 0
                        ? invoiceReceiptArchiveService.GetFileName(invoice.InvoiceNumber)
                        : null,
                    "Expense receipts are attached in a separate ZIP file.",
                    "Additional message:"));
            }
            catch (InvoiceEmailPreparationException exception)
            {
                return EndpointSupport.ValidationProblem(exception.Field, exception.Message);
            }
        });

        group.MapGet("/{id:guid}/email-receipts", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceEmailPreparationService invoiceEmailPreparationService,
            IInvoiceReceiptArchiveService invoiceReceiptArchiveService,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await LoadVisibleInvoiceAsync(db, id, userId, cancellationToken);
            if (invoice is null)
            {
                return Results.NotFound();
            }

            try
            {
                var preparation = await invoiceEmailPreparationService.PrepareAsync(
                    invoice, userId, null, includeReceipts: true, cancellationToken: cancellationToken);
                if (preparation.DeliveryRequest.ExpenseReceiptAttachments.Count == 0)
                {
                    return EndpointSupport.ValidationProblem("attachments", "No receipt attachments are available for this invoice.");
                }

                var archive = await invoiceReceiptArchiveService.CreateAsync(
                    preparation.DeliveryRequest,
                    cancellationToken);
                return Results.File(archive.Content, archive.ContentType, archive.FileName);
            }
            catch (InvoiceEmailPreparationException exception)
            {
                return EndpointSupport.ValidationProblem(exception.Field, exception.Message);
            }
        });

        group.MapPost("/{id:guid}/send-email", async (
            Guid id,
            InvoiceEmailDeliveryRequest? request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceDeliveryService invoiceDeliveryService,
            IInvoiceEmailPreparationService invoiceEmailPreparationService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("InvoiceEndpoints");
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await LoadVisibleInvoiceAsync(db, id, userId, cancellationToken);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            InvoiceEmailPreparation preparation;
            try
            {
                preparation = await invoiceEmailPreparationService.PrepareAsync(
                    invoice, userId, request?.Message, request?.IncludeReceipts is true, cancellationToken);
            }
            catch (InvoiceEmailPreparationException exception)
            {
                return EndpointSupport.ValidationProblem(exception.Field, exception.Message);
            }

            try
            {
                var deliveryRequest = preparation.DeliveryRequest;
                await invoiceDeliveryService.DeliverAsync(
                    InvoiceDeliveryChannel.Email,
                    invoice,
                    deliveryRequest.Client,
                    userId,
                    deliveryRequest.Message,
                    deliveryRequest.EmailSubject,
                    deliveryRequest.EmailBodyTemplate,
                    deliveryRequest.BusinessName,
                    deliveryRequest.AttachmentFileName,
                    deliveryRequest.SenderIdentity,
                    cancellationToken,
                    deliveryRequest.ExpenseReceiptAttachments);
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

            var invoicePdfResult = await invoicePdfService.OpenCurrentReadAsync(invoice, cancellationToken);
            if (!invoicePdfResult.IsAvailable)
            {
                return EndpointSupport.ValidationProblem("pdf", invoicePdfResult.UnavailableMessage!);
            }
            await invoicePdfResult.Pdf!.Content.DisposeAsync();

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
                    null,
                    null,
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

    private static string FormatBytes(long byteCount)
    {
        const decimal oneMegabyte = 1024m * 1024m;
        return byteCount < oneMegabyte
            ? $"{byteCount} bytes"
            : $"{byteCount / oneMegabyte:0.##} MB";
    }

    private static Task<Invoice?> LoadVisibleInvoiceAsync(
        AppDbContext db,
        Guid id,
        Guid? userId,
        CancellationToken cancellationToken) =>
        db.Invoices
            .WhereVisibleTo(userId)
            .Include(value => value.Client)
            .Include(value => value.Lines)
            .FirstOrDefaultAsync(value => value.Id == id, cancellationToken);

    private sealed record InvoiceEmailDeliveryRequest(string? Message, bool IncludeReceipts = false);

    private sealed record InvoiceEmailReviewResponse(
        string RecipientName,
        string RecipientEmail,
        string Subject,
        string PlainTextBody,
        string PdfFileName,
        long PdfSizeBytes,
        int ReceiptCount,
        string? ReceiptZipFileName,
        string ReceiptNote,
        string AdditionalMessageHeading);

    private sealed record InvoiceGoogleDrivePublishResponse(
        Invoice Invoice,
        string? FileId,
        string? FileName,
        string? WebViewLink);
}
