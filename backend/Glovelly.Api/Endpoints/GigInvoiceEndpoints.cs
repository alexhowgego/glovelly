using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class GigInvoiceEndpoints
{
    public static RouteGroupBuilder MapGigInvoiceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/generate-invoice", async (
            GenerateInvoiceFromGigSelectionRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gigIds = request.GigIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (gigIds.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["gigIds"] = ["Select at least one gig."]
                });
            }

            var gigs = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Client)
                .Include(value => value.Expenses)
                .Where(value => gigIds.Contains(value.Id))
                .OrderBy(value => value.Date)
                .ThenBy(value => value.Title)
                .ToListAsync();

            if (gigs.Count != gigIds.Count)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["gigIds"] = ["One or more selected gigs do not exist."]
                });
            }

            if (gigs.Any(gig => gig.InvoiceId.HasValue))
            {
                return Results.Conflict(new
                {
                    message = "All selected gigs must be uninvoiced before creating a combined invoice.",
                });
            }

            var distinctClientIds = gigs
                .Select(gig => gig.ClientId)
                .Distinct()
                .ToList();

            if (distinctClientIds.Count != 1)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["gigIds"] = ["Selected gigs must all belong to the same client."]
                });
            }

            var client = gigs[0].Client;
            if (client is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            var firstGig = gigs[0];
            var invoice = await invoiceWorkflowService.GenerateInvoiceForGigAsync(firstGig, client, userId);

            foreach (var gig in gigs.Skip(1))
            {
                gig.InvoiceId = invoice.Id;
                gig.InvoicedAt = DateTimeOffset.UtcNow;
                EndpointSupport.StampUpdate(gig, userId);
                await invoiceWorkflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, userId);
            }

            await db.SaveChangesAsync();

            var refreshedInvoice = await db.Invoices
                .WhereVisibleTo(userId)
                .Include(value => value.Lines)
                .FirstAsync(value => value.Id == invoice.Id);

            await invoiceWorkflowService.RedraftInvoiceAsync(refreshedInvoice, client, userId);
            await db.SaveChangesAsync();

            return Results.Created($"/invoices/{refreshedInvoice.Id}", refreshedInvoice);
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
                .WhereVisibleTo(userId)
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

        return group;
    }

    private sealed record GenerateInvoiceFromGigSelectionRequest(IReadOnlyList<Guid> GigIds);
}
