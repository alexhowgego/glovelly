using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class GigCrudEndpoints
{
    public static RouteGroupBuilder MapGigCrudEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gigs = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(gig => gig.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .OrderBy(gig => gig.Date)
                .ThenBy(gig => gig.Title)
                .ToListAsync();

            return Results.Ok(gigs);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstOrDefaultAsync(gig => gig.Id == id);

            return gig is null ? Results.NotFound() : Results.Ok(gig);
        });

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            GigStatusUpdateRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (gig is null)
            {
                return Results.NotFound();
            }

            if (!Enum.IsDefined(request.Status))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Status is invalid."]
                });
            }

            gig.Status = request.Status;
            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();

            return Results.Ok(gig);
        });

        group.MapPost("/", async (
            Gig gig,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gigValidation = EndpointSupport.ValidateGigRequest(gig);
            if (gigValidation is not null)
            {
                return gigValidation;
            }

            if (!await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == gig.ClientId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            if (gig.InvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .WhereVisibleTo(userId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == gig.InvoiceId.Value);

                if (invoice is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice does not exist."]
                    });
                }

                if (invoice.ClientId != gig.ClientId)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice client must match the gig client."]
                    });
                }
            }

            gig.Id = Guid.NewGuid();
            gig.Title = gig.Title.Trim();
            gig.Venue = gig.Venue.Trim();
            gig.Notes = gig.Notes?.Trim();
            gig.Client = null;
            gig.Invoice = null;
            gig.Expenses = EndpointSupport.NormalizeGigExpenses(gig.Expenses);
            gig.InvoicedAt = EndpointSupport.ResolveInvoicedAt(gig.InvoiceId, null, null, gig.InvoicedAt);
            EndpointSupport.StampCreate(gig, userId);

            db.Gigs.Add(gig);
            await invoiceWorkflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, userId);
            await db.SaveChangesAsync();

            return Results.Created($"/gigs/{gig.Id}", gig);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            Gig request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (gig is null)
            {
                return Results.NotFound();
            }

            var gigValidation = EndpointSupport.ValidateGigRequest(request);
            if (gigValidation is not null)
            {
                return gigValidation;
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

            var requestedInvoiceId = request.InvoiceId ?? gig.InvoiceId;

            if (requestedInvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .WhereVisibleTo(userId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == requestedInvoiceId.Value);

                if (invoice is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice does not exist."]
                    });
                }

                if (invoice.ClientId != request.ClientId)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice client must match the gig client."]
                    });
                }
            }

            var normalizedExpenses = EndpointSupport.NormalizeGigExpenses(request.Expenses, preserveIds: false);
            var previousInvoiceId = gig.InvoiceId;
            var existingExpenses = gig.Expenses
                .OrderBy(expense => expense.SortOrder)
                .ThenBy(expense => expense.Description)
                .ToList();

            gig.ClientId = request.ClientId;
            gig.InvoiceId = requestedInvoiceId;
            gig.Title = request.Title.Trim();
            gig.Date = request.Date;
            gig.Venue = request.Venue.Trim();
            gig.Fee = request.Fee;
            gig.TravelMiles = request.TravelMiles;
            gig.PassengerCount = request.PassengerCount;
            gig.Notes = request.Notes?.Trim();
            gig.WasDriving = request.WasDriving;
            gig.Status = request.Status;
            gig.InvoicedAt = EndpointSupport.ResolveInvoicedAt(
                requestedInvoiceId,
                previousInvoiceId,
                gig.InvoicedAt,
                request.InvoicedAt);

            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();

            var sharedExpenseCount = Math.Min(existingExpenses.Count, normalizedExpenses.Count);
            for (var i = 0; i < sharedExpenseCount; i++)
            {
                existingExpenses[i].SortOrder = normalizedExpenses[i].SortOrder;
                existingExpenses[i].Description = normalizedExpenses[i].Description;
                existingExpenses[i].Amount = normalizedExpenses[i].Amount;
            }

            if (existingExpenses.Count > normalizedExpenses.Count)
            {
                var expensesToRemove = existingExpenses.Skip(normalizedExpenses.Count).ToList();
                foreach (var attachment in expensesToRemove.SelectMany(expense => expense.Attachments).ToList())
                {
                    await attachmentStore.DeleteAsync(attachment.StorageKey);
                }

                db.GigExpenses.RemoveRange(expensesToRemove);
            }

            foreach (var expense in normalizedExpenses.Skip(sharedExpenseCount))
            {
                expense.GigId = gig.Id;
                db.GigExpenses.Add(expense);
            }

            await db.SaveChangesAsync();

            gig = await db.Gigs
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstAsync(value => value.Id == id);

            return Results.Ok(gig);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstOrDefaultAsync(gig => gig.Id == id);
            if (gig is null)
            {
                return Results.NotFound();
            }

            if (gig.Status != GigStatus.Confirmed)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Only planned gigs can be deleted."]
                });
            }

            if (gig.InvoiceId.HasValue)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["invoiceId"] = ["Gigs with linked invoices cannot be deleted."]
                });
            }

            foreach (var attachment in gig.Expenses.SelectMany(expense => expense.Attachments).ToList())
            {
                await attachmentStore.DeleteAsync(attachment.StorageKey);
            }

            _ = await invoiceWorkflowService.RemoveSystemGeneratedInvoiceLinesForGigAsync(gig.Id);
            db.Gigs.Remove(gig);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return group;
    }

    private sealed record GigStatusUpdateRequest(GigStatus Status);
}
