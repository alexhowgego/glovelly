using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

// Saved-receipt correction only. Source acquisition and creation belong to intake.
internal static class GigReceiptEndpoints
{
    public static RouteGroupBuilder MapGigReceiptEndpoints(this RouteGroupBuilder group)
    {
        group.MapPatch("/receipt-drafts/{expenseId:guid}", async (
            Guid expenseId, ReceiptUpdate request, AppDbContext db, ClaimsPrincipal user,
            ICurrentUserAccessor currentUser, IWorkspaceEventPublisher events, IInvoiceWorkflowService invoices) =>
        {
            var userId = currentUser.TryGetUserId(user);
            var targetGig = await db.Gigs.WhereVisibleTo(userId).Include(value => value.Expenses).FirstOrDefaultAsync(value => value.Id == request.GigId);
            if (targetGig is null) return EndpointSupport.ValidationProblem("gigId", "Gig does not exist.");
            var expense = await FindExpenseAsync(db, expenseId, userId);
            if (expense is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Description)) return EndpointSupport.ValidationProblem("description", "Expense description is required.");
            if (request.Amount < 0) return EndpointSupport.ValidationProblem("amount", "Expense amount cannot be negative.");
            if (request.Category.HasValue && !Enum.IsDefined(request.Category.Value)) return EndpointSupport.ValidationProblem("category", "Expense category is invalid.");

            var previousGig = expense.Gig!;
            var moved = previousGig.Id != targetGig.Id;
            expense.Description = request.Description.Trim();
            expense.Amount = request.Amount;
            expense.Category = request.Category;
            if (moved)
            {
                expense.GigId = targetGig.Id;
                expense.Gig = targetGig;
                expense.SortOrder = targetGig.Expenses.Count == 0 ? 1 : targetGig.Expenses.Max(value => value.SortOrder) + 1;
            }
            EndpointSupport.StampUpdate(previousGig, userId);
            EndpointSupport.StampUpdate(targetGig, userId);
            await db.SaveChangesAsync();
            var affected = await db.Gigs.WhereVisibleTo(userId).IncludeGigDetails()
                .Where(value => value.Id == targetGig.Id || value.Id == previousGig.Id).ToListAsync();
            var refreshed = await RefreshAsync(affected, userId, invoices, events);
            return Results.Ok(new
            {
                gig = affected.First(value => value.Id == targetGig.Id),
                previousGig = moved ? affected.First(value => value.Id == previousGig.Id) : null,
                expenseId, moved, invoices = refreshed,
            });
        });

        group.MapDelete("/receipt-drafts/{expenseId:guid}", async (
            Guid expenseId, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUser,
            IExpenseAttachmentStore store, IInvoiceWorkflowService invoices, IWorkspaceEventPublisher events) =>
        {
            var userId = currentUser.TryGetUserId(user);
            var expense = await FindExpenseAsync(db, expenseId, userId);
            if (expense is null) return Results.NotFound();
            var gig = expense.Gig!;
            var keys = expense.Attachments.Select(value => value.StorageKey).ToList();
            db.GigExpenses.Remove(expense);
            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();
            foreach (var key in keys) await store.DeleteAsync(key);
            var savedGig = await db.Gigs.WhereVisibleTo(userId).IncludeGigDetails().SingleAsync(value => value.Id == gig.Id);
            var refreshed = await RefreshAsync([savedGig], userId, invoices, events);
            return Results.Ok(new { gig = savedGig, invoices = refreshed });
        });
        return group;
    }

    private static Task<GigExpense?> FindExpenseAsync(AppDbContext db, Guid expenseId, Guid? userId) =>
        db.GigExpenses.Include(value => value.Attachments).Include(value => value.Gig)
            .FirstOrDefaultAsync(value => value.Id == expenseId && value.Gig != null && (value.Gig.CreatedByUserId == null || value.Gig.CreatedByUserId == userId));

    private static async Task<IReadOnlyList<Invoice>> RefreshAsync(List<Gig> gigs, Guid? userId, IInvoiceWorkflowService invoices, IWorkspaceEventPublisher events)
    {
        var refreshed = await invoices.RefreshDraftInvoicesForGigsAsync(gigs, userId);
        foreach (var gig in gigs) await events.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gig.Id, DateTimeOffset.UtcNow));
        foreach (var invoice in refreshed) await events.PublishAsync(userId, new WorkspaceEvent("invoices", "updated", invoice.Id, DateTimeOffset.UtcNow));
        return refreshed;
    }

    private sealed record ReceiptUpdate(Guid GigId, string Description, decimal Amount, GigExpenseCategory? Category);
}
