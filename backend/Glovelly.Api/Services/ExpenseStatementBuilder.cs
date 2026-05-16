using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class ExpenseStatementBuilder(AppDbContext dbContext) : IExpenseStatementBuilder
{
    public async Task<ExpenseStatementProjection> BuildAsync(
        ExpenseStatementRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateRequest(request);
        if (errors.Count > 0)
        {
            throw new ExpenseStatementValidationException(errors);
        }

        var requestedGigIds = request.GigIds!.Distinct().ToList();
        var client = await dbContext.Clients
            .AsNoTracking()
            .Where(client => client.CreatedByUserId == null || client.CreatedByUserId == userId)
            .FirstOrDefaultAsync(client => client.Id == request.ClientId, cancellationToken);

        if (client is null)
        {
            throw new ExpenseStatementValidationException(new Dictionary<string, string[]>
            {
                ["clientId"] = ["Client does not exist."]
            });
        }

        var gigs = await dbContext.Gigs
            .AsNoTracking()
            .Include(gig => gig.Expenses)
                .ThenInclude(expense => expense.Attachments)
            .Where(gig => gig.CreatedByUserId == null || gig.CreatedByUserId == userId)
            .Where(gig => requestedGigIds.Contains(gig.Id))
            .OrderBy(gig => gig.Date)
            .ThenBy(gig => gig.Title)
            .ToListAsync(cancellationToken);

        if (gigs.Count != requestedGigIds.Count)
        {
            throw new ExpenseStatementValidationException(new Dictionary<string, string[]>
            {
                ["gigIds"] = ["One or more gigs do not exist."]
            });
        }

        if (gigs.Any(gig => gig.ClientId != request.ClientId))
        {
            throw new ExpenseStatementValidationException(new Dictionary<string, string[]>
            {
                ["gigIds"] = ["All selected gigs must belong to the selected client."]
            });
        }

        var requestedExpenseIds = request.ExpenseIds?.Distinct().ToHashSet();
        var statementGigs = gigs
            .Select(gig =>
            {
                var expenses = gig.Expenses
                    .Where(expense => request.IncludeReimbursedExpenses || !gig.InvoiceId.HasValue)
                    .Where(expense => requestedExpenseIds is null || requestedExpenseIds.Contains(expense.Id))
                    .Where(expense => expense.Amount != 0)
                    .OrderBy(expense => expense.SortOrder)
                    .ThenBy(expense => expense.Description)
                    .Select(expense => new ExpenseStatementExpense(
                        expense.Id,
                        expense.Description,
                        expense.Amount,
                        expense.SortOrder,
                        request.IncludeReceiptAttachments
                            ? expense.Attachments
                                .OrderBy(attachment => attachment.CreatedAt)
                                .Select(attachment => new ExpenseStatementAttachment(
                                    attachment.Id,
                                    attachment.FileName,
                                    attachment.ContentType,
                                    attachment.SizeBytes,
                                    attachment.CreatedAt))
                                .ToList()
                            : []))
                    .ToList();

                return new ExpenseStatementGig(
                    gig.Id,
                    gig.Title,
                    gig.Date,
                    gig.Venue,
                    gig.InvoiceId.HasValue,
                    expenses,
                    expenses.Sum(expense => expense.Amount));
            })
            .Where(gig => gig.Expenses.Count > 0)
            .ToList();

        if (requestedExpenseIds is not null)
        {
            var includedExpenseIds = statementGigs
                .SelectMany(gig => gig.Expenses)
                .Select(expense => expense.ExpenseId)
                .ToHashSet();

            if (!requestedExpenseIds.IsSubsetOf(includedExpenseIds))
            {
                throw new ExpenseStatementValidationException(new Dictionary<string, string[]>
                {
                    ["expenseIds"] = ["One or more expenses do not exist, are reimbursed, or do not belong to the selected gigs."]
                });
            }
        }

        return new ExpenseStatementProjection(
            client.Id,
            client.Name,
            DateOnly.FromDateTime(DateTime.UtcNow),
            statementGigs,
            statementGigs.Sum(gig => gig.Total),
            statementGigs.Sum(gig => gig.Expenses.Count),
            statementGigs.Sum(gig => gig.Expenses.Sum(expense => expense.Attachments.Count)));
    }

    private static Dictionary<string, string[]> ValidateRequest(ExpenseStatementRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.ClientId == Guid.Empty)
        {
            errors["clientId"] = ["Client is required."];
        }

        if (request.GigIds is null || request.GigIds.Count == 0 || request.GigIds.Any(gigId => gigId == Guid.Empty))
        {
            errors["gigIds"] = ["At least one gig is required."];
        }

        if (request.ExpenseIds?.Any(expenseId => expenseId == Guid.Empty) is true)
        {
            errors["expenseIds"] = ["Expense ids cannot be empty."];
        }

        return errors;
    }
}
