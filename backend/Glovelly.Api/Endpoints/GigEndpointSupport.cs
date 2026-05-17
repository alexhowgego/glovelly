using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class GigEndpointSupport
{
    public static Task<GigExpense?> FindVisibleExpenseAsync(
        AppDbContext db,
        Guid? userId,
        Guid gigId,
        Guid expenseId,
        bool asNoTracking)
    {
        var query = db.GigExpenses
            .Include(expense => expense.Attachments)
            .Include(expense => expense.Gig)
            .Where(expense => expense.Id == expenseId && expense.GigId == gigId)
            .Where(expense => expense.Gig != null
                && (expense.Gig.CreatedByUserId == null || expense.Gig.CreatedByUserId == userId));

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync();
    }

    public static Task<ExpenseAttachment?> FindVisibleAttachmentAsync(
        AppDbContext db,
        Guid? userId,
        Guid gigId,
        Guid expenseId,
        Guid attachmentId,
        bool asNoTracking)
    {
        var query = db.ExpenseAttachments
            .Include(attachment => attachment.Expense)
                .ThenInclude(expense => expense!.Gig)
            .Where(attachment => attachment.Id == attachmentId
                && attachment.GigExpenseId == expenseId
                && attachment.Expense != null
                && attachment.Expense.GigId == gigId
                && attachment.Expense.Gig != null
                && (attachment.Expense.Gig.CreatedByUserId == null
                    || attachment.Expense.Gig.CreatedByUserId == userId));

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync();
    }

    public static IResult? ValidateAttachmentFile(IFormFile? file, ExpenseAttachmentSettings settings)
    {
        if (file is null || file.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Upload a receipt file."]
            });
        }

        if (file.Length > settings.MaxFileSizeBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [$"Receipt files must be {settings.MaxFileSizeBytes / 1024 / 1024} MB or smaller."]
            });
        }

        if (!settings.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Receipt files must be PDF, JPG, PNG, WebP or HEIC."]
            });
        }

        return null;
    }

    public static string BuildAttachmentStorageKey(Guid? userId, Guid gigId, Guid expenseId, Guid attachmentId)
    {
        var owner = userId?.ToString("N") ?? "system";
        return $"users/{owner}/gigs/{gigId:N}/expenses/{expenseId:N}/attachments/{attachmentId:N}";
    }
}
