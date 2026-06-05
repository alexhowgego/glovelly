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

        group.MapGet("/{id:guid}/pdf", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoicePdfService invoicePdfService,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Client)
                .FirstOrDefaultAsync(value => value.Id == id, cancellationToken);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            var pdf = await invoicePdfService.OpenReadAsync(invoice, cancellationToken);
            if (pdf is null)
            {
                return Results.NotFound();
            }

            var userDefaultPattern = userId.HasValue
                ? await db.Users
                    .AsNoTracking()
                    .Where(value => value.Id == userId.Value && value.IsActive)
                    .Select(value => value.InvoiceFilenamePattern)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var periodDate = await InvoiceEndpointSupport.ResolveInvoicePeriodDateAsync(db, invoice.Id, cancellationToken);

            return Results.Stream(
                pdf.Content,
                pdf.ContentType,
                InvoicePdfFilenameBuilder.Build(
                    invoice,
                    invoice.Client,
                    userDefaultPattern,
                    periodDate));
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
                return EndpointSupport.ValidationProblem("clientId", "Client does not exist.");
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
            invoice.PdfStorageKey = null;
            invoice.PdfFileName = null;
            invoice.PdfContentType = null;
            invoice.PdfSizeBytes = null;
            invoice.PdfGeneratedAt = null;
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
                return EndpointSupport.ValidationProblem("clientId", "Client does not exist.");
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
                return EndpointSupport.ValidationProblem("clientId", "Invoice client must match any linked gig line clients.");
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
                    return EndpointSupport.ValidationProblem("clientId", "Client does not exist.");
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
                return EndpointSupport.ValidationProblem("status", "Draft invoices can be redrafted, but cannot be re-issued until they have been issued.");
            }

            if (invoice.Status is InvoiceStatus.Cancelled)
            {
                return EndpointSupport.ValidationProblem("status", "Cancelled invoices must be moved back to Draft before they can be redrafted.");
            }

            var client = await db.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == invoice.ClientId);

            if (client is null)
            {
                return EndpointSupport.ValidationProblem("clientId", "Client does not exist.");
            }

            if (string.IsNullOrWhiteSpace(client.Email))
            {
                return EndpointSupport.ValidationProblem("recipient", "Invoice recipient email is missing.");
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
                return EndpointSupport.ValidationProblem("status", $"{invoice.Status} invoices must be re-issued rather than redrafted.");
            }

            var client = await db.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == invoice.ClientId);

            if (client is null)
            {
                return EndpointSupport.ValidationProblem("clientId", "Client does not exist.");
            }

            var linkedGigs = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                .Where(value => value.InvoiceId == invoice.Id)
                .ToListAsync();

            foreach (var gig in linkedGigs)
            {
                await invoiceWorkflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, userId);
            }

            if (linkedGigs.Count > 0)
            {
                await db.SaveChangesAsync();
                db.Entry(invoice)
                    .Collection(value => value.Lines)
                    .IsLoaded = false;
                await db.Entry(invoice)
                    .Collection(value => value.Lines)
                    .LoadAsync();
            }

            await invoiceWorkflowService.RedraftInvoiceAsync(invoice, client, userId);
            await db.SaveChangesAsync();

            return Results.Ok(invoice);
        });

        group.MapInvoiceDeliveryEndpoints();

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
                return EndpointSupport.ValidationProblem("reason", "Adjustment reason is required.");
            }

            if (request.Amount == 0)
            {
                return EndpointSupport.ValidationProblem("amount", "Adjustment amount must be non-zero.");
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
            IInvoicePdfService invoicePdfService,
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
                return EndpointSupport.ValidationProblem("status", $"Only Draft invoices can be deleted. {invoice.Status} invoices must be retained for reporting.");
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

            await invoicePdfService.DeleteAsync(invoice);

            db.Invoices.Remove(invoice);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return group;
    }

    private sealed record InvoiceStatusUpdateRequest(InvoiceStatus Status);
    private sealed record InvoiceAdjustmentCreateRequest(decimal Amount, string Reason);
}
