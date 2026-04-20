using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

public static class GigEndpoints
{
    public static RouteGroupBuilder MapGigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db) =>
        {
            var gigs = await db.Gigs
                .AsNoTracking()
                .Include(gig => gig.Expenses)
                .OrderBy(gig => gig.Date)
                .ThenBy(gig => gig.Title)
                .ToListAsync();

            return Results.Ok(gigs);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var gig = await db.Gigs
                .AsNoTracking()
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(gig => gig.Id == id);

            return gig is null ? Results.NotFound() : Results.Ok(gig);
        });

        group.MapPost("/{id:guid}/generate-invoice", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .Include(value => value.Client)
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (gig is null)
            {
                return Results.NotFound();
            }

            if (gig.InvoiceId.HasValue)
            {
                return Results.Conflict(new
                {
                    message = "This gig has already been invoiced.",
                    invoiceId = gig.InvoiceId,
                });
            }

            var client = gig.Client;
            if (client is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            var invoice = await invoiceWorkflowService.GenerateInvoiceForGigAsync(gig, client, userId);
            await db.SaveChangesAsync();

            return Results.Created($"/invoices/{invoice.Id}", invoice);
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
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .Include(value => value.Expenses)
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

            if (request.InvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == request.InvoiceId.Value);

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
            gig.InvoiceId = request.InvoiceId;
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
                request.InvoiceId,
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
                .FirstAsync(value => value.Id == id);

            await invoiceWorkflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, userId);
            await db.SaveChangesAsync();

            return Results.Ok(gig);
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var gig = await db.Gigs
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(gig => gig.Id == id);
            if (gig is null)
            {
                return Results.NotFound();
            }

            _ = await invoiceWorkflowService.RemoveSystemGeneratedInvoiceLinesForGigAsync(gig.Id);
            db.Gigs.Remove(gig);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return group;
    }
}
