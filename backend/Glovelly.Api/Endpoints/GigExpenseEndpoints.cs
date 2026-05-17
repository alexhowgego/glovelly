using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class GigExpenseEndpoints
{
    public static RouteGroupBuilder MapGigExpenseEndpoints(this RouteGroupBuilder group)
    {
        group.MapPatch("/{gigId:guid}/expenses/reimbursement", async (
            Guid gigId,
            GigExpenseReimbursementUpdateRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var expenseIds = (request.ExpenseIds ?? [])
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (expenseIds.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["expenseIds"] = ["Select at least one expense."]
                });
            }

            if (!Enum.IsDefined(request.Status))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Reimbursement status is invalid."]
                });
            }

            var method = request.Method?.Trim();
            var note = request.Note?.Trim();
            if (request.Status is GigExpenseReimbursementStatus.Reimbursed)
            {
                if (!request.ReimbursedAt.HasValue)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["reimbursedAt"] = ["Reimbursed date is required."]
                    });
                }

                if (string.IsNullOrWhiteSpace(method) && string.IsNullOrWhiteSpace(note))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["note"] = ["Record a reimbursement method or note."]
                    });
                }
            }

            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(value => value.Id == gigId);

            if (gig is null)
            {
                return Results.NotFound();
            }

            if (request.LinkedInvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .WhereVisibleTo(userId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == request.LinkedInvoiceId.Value);

                if (invoice is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["linkedInvoiceId"] = ["Linked invoice does not exist."]
                    });
                }

                if (invoice.ClientId != gig.ClientId)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["linkedInvoiceId"] = ["Linked invoice client must match the gig client."]
                    });
                }
            }

            var expenses = gig.Expenses
                .Where(expense => expenseIds.Contains(expense.Id))
                .ToList();

            if (expenses.Count != expenseIds.Count)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["expenseIds"] = ["One or more expenses do not belong to this gig."]
                });
            }

            foreach (var expense in expenses)
            {
                ApplyReimbursementUpdate(expense, request, method, note, userId);
            }

            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();

            var savedGig = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstAsync(value => value.Id == gigId);

            return Results.Ok(savedGig);
        });

        return group;
    }

    private static void ApplyReimbursementUpdate(
        GigExpense expense,
        GigExpenseReimbursementUpdateRequest request,
        string? method,
        string? note,
        Guid? userId)
    {
        expense.ReimbursementStatus = request.Status;
        expense.ReimbursementUpdatedAt = DateTimeOffset.UtcNow;
        expense.ReimbursementUpdatedByUserId = userId;

        if (request.Status is GigExpenseReimbursementStatus.Unreimbursed)
        {
            expense.ReimbursedAt = null;
            expense.ReimbursementMethod = null;
            expense.ReimbursementNote = null;
            expense.ReimbursementInvoiceId = null;
            return;
        }

        expense.ReimbursedAt = request.Status is GigExpenseReimbursementStatus.Reimbursed
            ? request.ReimbursedAt
            : null;
        expense.ReimbursementMethod = string.IsNullOrWhiteSpace(method) ? null : method;
        expense.ReimbursementNote = string.IsNullOrWhiteSpace(note) ? null : note;
        expense.ReimbursementInvoiceId = request.LinkedInvoiceId;
    }

    // Reimbursement metadata tracks external recovery of an expense. It is not an invoice
    // adjustment: adjustments change invoice totals, while this state controls whether
    // an existing gig expense should be claimable by default in future generated documents.
    private sealed record GigExpenseReimbursementUpdateRequest(
        IReadOnlyList<Guid>? ExpenseIds,
        GigExpenseReimbursementStatus Status,
        DateTimeOffset? ReimbursedAt,
        string? Method,
        string? Note,
        Guid? LinkedInvoiceId);
}
