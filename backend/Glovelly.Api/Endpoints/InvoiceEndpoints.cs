using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

public static class InvoiceEndpoints
{
    public static RouteGroupBuilder MapInvoiceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db) =>
        {
            var invoices = await db.Invoices
                .AsNoTracking()
                .Include(invoice => invoice.Lines)
                .OrderByDescending(invoice => invoice.InvoiceDate)
                .ThenBy(invoice => invoice.InvoiceNumber)
                .ToListAsync();

            return Results.Ok(invoices);
        });

        group.MapGet("/{id:guid}/pdf", async (Guid id, AppDbContext db) =>
        {
            var invoice = await db.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == id);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            if (invoice.PdfBlob is null || invoice.PdfBlob.Length == 0)
            {
                return Results.NotFound();
            }

            return Results.File(invoice.PdfBlob, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var invoice = await db.Invoices
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

            invoice.Id = Guid.NewGuid();
            invoice.InvoiceNumber = invoice.InvoiceNumber.Trim();
            invoice.Description = invoice.Description?.Trim();
            invoice.Client = null;
            invoice.Lines = new List<InvoiceLine>();
            EndpointSupport.StampCreate(invoice, userId);

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            return Results.Created($"/invoices/{invoice.Id}", invoice);
        });

        group.MapPut("/{id:guid}", async (Guid id, Invoice request, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            if (!await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == request.ClientId))
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
            invoice.InvoiceDate = request.InvoiceDate;
            invoice.DueDate = request.DueDate;
            var requestedStatus = request.Status;
            var statusValidation = EndpointSupport.ValidateInvoiceStatusTransition(invoice.Status, requestedStatus);
            if (statusValidation is not null)
            {
                return statusValidation;
            }

            if (invoice.Status != requestedStatus)
            {
                invoice.StatusUpdatedUtc = DateTimeOffset.UtcNow;
            }

            invoice.Status = request.Status;
            invoice.Description = request.Description?.Trim();
            invoice.PdfBlob = request.PdfBlob;
            EndpointSupport.StampUpdate(invoice, userId);

            await db.SaveChangesAsync();

            return Results.Ok(invoice);
        });

        group.MapPut("/{id:guid}/status", async (
            Guid id,
            InvoiceStatusUpdateRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var invoice = await db.Invoices
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

            invoice.Status = request.Status;
            invoice.StatusUpdatedUtc = DateTimeOffset.UtcNow;
            EndpointSupport.StampUpdate(invoice, currentUserAccessor.TryGetUserId(user));
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
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (invoice is null)
            {
                return Results.NotFound();
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

        group.MapPost("/{id:guid}/adjustments", async (
            Guid id,
            InvoiceAdjustmentCreateRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var invoice = await db.Invoices
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

            var userId = currentUserAccessor.TryGetUserId(user);
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
            var invoice = await db.Invoices.FirstOrDefaultAsync(invoice => invoice.Id == id);
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

            var userId = currentUserAccessor.TryGetUserId(user);
            var deletedAtUtc = DateTimeOffset.UtcNow;
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
}
