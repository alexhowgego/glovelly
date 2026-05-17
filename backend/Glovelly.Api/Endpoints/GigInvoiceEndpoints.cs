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
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var result = await invoiceWorkflowService.GenerateInvoiceFromGigSelectionAsync(request.GigIds, userId);

            return result.Status switch
            {
                GenerateInvoiceFromGigSelectionStatus.Created =>
                    Results.Created($"/invoices/{result.Invoice!.Id}", result.Invoice),
                GenerateInvoiceFromGigSelectionStatus.Conflict =>
                    Results.Conflict(new
                    {
                        message = result.ConflictMessage,
                    }),
                GenerateInvoiceFromGigSelectionStatus.ValidationFailed =>
                    Results.ValidationProblem(result.ValidationErrors!),
                _ => Results.Problem()
            };
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
