using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

public static class InvoiceEndpoints
{
    private const int DefaultPaymentWindowDays = 14;

    public static RouteGroupBuilder MapInvoiceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoices = await db.Invoices
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(invoice => invoice.Lines)
                .OrderByDescending(invoice => invoice.InvoiceDate)
                .ThenBy(invoice => invoice.InvoiceNumber)
                .ToListAsync();

            return Results.Ok(invoices);
        });

        group.MapGet("/{id:guid}/pdf", async (Guid id, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Client)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            if (invoice.PdfBlob is null || invoice.PdfBlob.Length == 0)
            {
                return Results.NotFound();
            }

            var userDefaultPattern = userId.HasValue
                ? await db.Users
                    .AsNoTracking()
                    .Where(value => value.Id == userId.Value && value.IsActive)
                    .Select(value => value.InvoiceFilenamePattern)
                    .FirstOrDefaultAsync()
                : null;

            return Results.File(
                invoice.PdfBlob,
                "application/pdf",
                InvoicePdfFilenameBuilder.Build(invoice, invoice.Client, userDefaultPattern));
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);

            return invoice is null ? Results.NotFound() : Results.Ok(invoice);
        });

        group.MapPost("/", async (Invoice invoice, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == invoice.ClientId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            var issuedOn = DateOnly.FromDateTime(DateTime.UtcNow);
            var paymentWindowDays = userId.HasValue
                ? await db.Users
                    .AsNoTracking()
                    .Where(value => value.Id == userId.Value && value.IsActive)
                    .Select(value => value.DefaultPaymentWindowDays)
                    .FirstOrDefaultAsync() ?? DefaultPaymentWindowDays
                : DefaultPaymentWindowDays;

            invoice.Id = Guid.NewGuid();
            invoice.InvoiceNumber = invoice.InvoiceNumber.Trim();
            invoice.InvoiceDate = issuedOn;
            invoice.DueDate = issuedOn.AddDays(paymentWindowDays);
            invoice.Description = invoice.Description?.Trim();
            invoice.Client = null;
            invoice.Lines = new List<InvoiceLine>();
            EndpointSupport.StampCreate(invoice, userId);

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            return Results.Created($"/invoices/{invoice.Id}", invoice);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            Invoice request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .Include(value => value.Client)
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            var requestClient = await db.Clients
                    .WhereVisibleTo(userId)
                    .FirstOrDefaultAsync(client => client.Id == request.ClientId);
            if (requestClient is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            var hasConflictingGigLinks = await db.InvoiceLines
                .Where(line => line.InvoiceId == invoice.Id && line.GigId.HasValue)
                .Join(
                    db.Gigs,
                    line => line.GigId!.Value,
                    gig => gig.Id,
                    (_, gig) => gig.ClientId)
                .AnyAsync(clientId => clientId != request.ClientId);

            if (hasConflictingGigLinks)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Invoice client must match any linked gig line clients."]
                });
            }

            invoice.InvoiceNumber = request.InvoiceNumber.Trim();
            invoice.ClientId = request.ClientId;
            var requestedStatus = request.Status;
            if (requestedStatus is not InvoiceStatus.Issued)
            {
                invoice.InvoiceDate = request.InvoiceDate;
                invoice.DueDate = request.DueDate;
            }
            invoice.Description = request.Description?.Trim();
            var statusValidation = EndpointSupport.ValidateInvoiceStatusTransition(invoice.Status, requestedStatus);
            if (statusValidation is not null)
            {
                return statusValidation;
            }

            if (invoice.Status != requestedStatus && requestedStatus is InvoiceStatus.Issued)
            {
                await invoiceWorkflowService.IssueInvoiceAsync(invoice, requestClient, userId);
            }
            else if (invoice.Status != requestedStatus)
            {
                invoice.StatusUpdatedUtc = DateTimeOffset.UtcNow;
                invoice.Status = request.Status;
            }
            if (requestedStatus is not InvoiceStatus.Issued)
            {
                invoice.PdfBlob = request.PdfBlob;
            }
            EndpointSupport.StampUpdate(invoice, userId);

            await db.SaveChangesAsync();

            return Results.Ok(invoice);
        });

        group.MapPut("/{id:guid}/status", async (
            Guid id,
            InvoiceStatusUpdateRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .Include(value => value.Client)
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);
            if (invoice is null)
            {
                return Results.NotFound();
            }

            var statusValidation = EndpointSupport.ValidateInvoiceStatusTransition(invoice.Status, request.Status);
            if (statusValidation is not null)
            {
                return statusValidation;
            }

            if (invoice.Status == request.Status)
            {
                return Results.Ok(invoice);
            }

            if (request.Status is InvoiceStatus.Issued)
            {
                if (invoice.Client is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["clientId"] = ["Client does not exist."]
                    });
                }

                await invoiceWorkflowService.IssueInvoiceAsync(invoice, invoice.Client, userId);
                await db.SaveChangesAsync();

                return Results.Ok(invoice);
            }

            invoice.Status = request.Status;
            invoice.StatusUpdatedUtc = DateTimeOffset.UtcNow;
            EndpointSupport.StampUpdate(invoice, userId);
            await db.SaveChangesAsync();

            return Results.Ok(invoice);
        });

        group.MapPost("/{id:guid}/reissue", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            if (invoice.Status is InvoiceStatus.Draft)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Draft invoices can be redrafted, but cannot be re-issued until they have been issued."]
                });
            }

            if (invoice.Status is InvoiceStatus.Cancelled)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Cancelled invoices must be moved back to Draft before they can be redrafted."]
                });
            }

            var client = await db.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == invoice.ClientId);

            if (client is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            if (string.IsNullOrWhiteSpace(client.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["recipient"] = ["Invoice recipient email is missing."]
                });
            }

            await invoiceWorkflowService.ReissueInvoiceAsync(invoice, client, userId);
            await db.SaveChangesAsync();

            return Results.Ok(invoice);
        });

        group.MapPost("/{id:guid}/redraft", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            if (invoice.Status is not InvoiceStatus.Draft)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"{invoice.Status} invoices must be re-issued rather than redrafted."]
                });
            }

            var client = await db.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == invoice.ClientId);

            if (client is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            await invoiceWorkflowService.RedraftInvoiceAsync(invoice, client, userId);
            await db.SaveChangesAsync();

            return Results.Ok(invoice);
        });

        group.MapPost("/{id:guid}/send-email", async (
            Guid id,
            InvoiceEmailDeliveryRequest? request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceDeliveryService invoiceDeliveryService,
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
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            if (string.IsNullOrWhiteSpace(invoice.Client.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["recipient"] = ["Invoice recipient email is missing."]
                });
            }

            if (invoice.PdfBlob is null || invoice.PdfBlob.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["pdf"] = ["Invoice PDF is missing."]
                });
            }

            var userDefaultPattern = userId.HasValue
                ? await db.Users
                    .AsNoTracking()
                    .Where(value => value.Id == userId.Value && value.IsActive)
                    .Select(value => value.InvoiceFilenamePattern)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var attachmentFileName = InvoicePdfFilenameBuilder.Build(
                invoice,
                invoice.Client,
                userDefaultPattern);
            var sendingUser = userId.HasValue
                ? await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        value => value.Id == userId.Value && value.IsActive,
                        cancellationToken)
                : null;
            var senderIdentity = InvoiceEmailSenderIdentityBuilder.Build(sendingUser);

            try
            {
                await invoiceDeliveryService.DeliverAsync(
                    InvoiceDeliveryChannel.Email,
                    invoice,
                    invoice.Client,
                    userId,
                    request?.Message,
                    attachmentFileName,
                    senderIdentity,
                    cancellationToken);
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
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            if (invoice.PdfBlob is null || invoice.PdfBlob.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["pdf"] = ["Invoice PDF is missing."]
                });
            }

            var userDefaultPattern = userId.HasValue
                ? await db.Users
                    .AsNoTracking()
                    .Where(value => value.Id == userId.Value && value.IsActive)
                    .Select(value => value.InvoiceFilenamePattern)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var attachmentFileName = InvoicePdfFilenameBuilder.Build(
                invoice,
                invoice.Client,
                userDefaultPattern);

            InvoiceDeliveryResult deliveryResult;
            try
            {
                deliveryResult = await invoiceDeliveryService.DeliverAsync(
                    InvoiceDeliveryChannel.GoogleDrive,
                    invoice,
                    invoice.Client,
                    userId,
                    message: null,
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

        group.MapPost("/{id:guid}/adjustments", async (
            Guid id,
            InvoiceAdjustmentCreateRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);
            if (invoice is null)
            {
                return Results.NotFound();
            }

            var reason = request.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["reason"] = ["Adjustment reason is required."]
                });
            }

            if (request.Amount == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["amount"] = ["Adjustment amount must be non-zero."]
                });
            }

            _ = await invoiceWorkflowService.CreateManualAdjustmentAsync(invoice, request.Amount, reason, userId);
            await db.SaveChangesAsync();

            return Results.Ok(invoice);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("InvoiceEndpoints");
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .FirstOrDefaultAsync(invoice => invoice.Id == id);
            if (invoice is null)
            {
                return Results.NotFound();
            }

            if (invoice.Status is not InvoiceStatus.Draft)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"Only Draft invoices can be deleted. {invoice.Status} invoices must be retained for reporting."]
                });
            }

            var deletedAtUtc = DateTimeOffset.UtcNow;
            var linkedGigs = await db.Gigs
                .WhereVisibleTo(userId)
                .Where(gig => gig.InvoiceId == invoice.Id)
                .ToListAsync();

            foreach (var gig in linkedGigs)
            {
                gig.InvoiceId = null;
                gig.Invoice = null;
                gig.InvoicedAt = null;
                EndpointSupport.StampUpdate(gig, userId);
            }

            logger.LogInformation(
                "Invoice {InvoiceId} ({InvoiceNumber}) deleted by user {UserId} at {DeletedAtUtc}.",
                invoice.Id,
                invoice.InvoiceNumber,
                userId,
                deletedAtUtc);

            db.Invoices.Remove(invoice);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return group;
    }

    private sealed record InvoiceStatusUpdateRequest(InvoiceStatus Status);
    private sealed record InvoiceAdjustmentCreateRequest(decimal Amount, string Reason);
    private sealed record InvoiceEmailDeliveryRequest(string? Message);
    private sealed record InvoiceGoogleDrivePublishResponse(
        Invoice Invoice,
        string? FileId,
        string? FileName,
        string? WebViewLink);
}
