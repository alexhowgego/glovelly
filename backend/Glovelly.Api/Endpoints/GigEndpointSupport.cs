using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class GigEndpointSupport
{
    public static IQueryable<Gig> IncludeGigDetails(this IQueryable<Gig> query)
    {
        return query
            .Include(gig => gig.ExternalResources)
                .ThenInclude(resource => resource.Attachments)
            .Include(gig => gig.Expenses)
                .ThenInclude(expense => expense.Attachments);
    }

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

    public static Task<GigExternalResource?> FindVisibleExternalResourceAsync(
        AppDbContext db,
        Guid? userId,
        Guid gigId,
        Guid resourceId,
        bool asNoTracking)
    {
        var query = db.GigExternalResources
            .Include(resource => resource.Attachments)
            .Include(resource => resource.Gig)
            .Where(resource => resource.Id == resourceId && resource.GigId == gigId)
            .Where(resource => resource.Gig != null
                && (resource.Gig.CreatedByUserId == null || resource.Gig.CreatedByUserId == userId));

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync();
    }

    public static Task<GigExternalResourceAttachment?> FindVisibleExternalResourceAttachmentAsync(
        AppDbContext db,
        Guid? userId,
        Guid gigId,
        Guid resourceId,
        Guid attachmentId,
        bool asNoTracking)
    {
        var query = db.GigExternalResourceAttachments
            .Include(attachment => attachment.Resource)
                .ThenInclude(resource => resource!.Gig)
            .Where(attachment => attachment.Id == attachmentId
                && attachment.GigExternalResourceId == resourceId
                && attachment.Resource != null
                && attachment.Resource.GigId == gigId
                && attachment.Resource.Gig != null
                && (attachment.Resource.Gig.CreatedByUserId == null
                    || attachment.Resource.Gig.CreatedByUserId == userId));

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync();
    }

    public static IResult? ValidateReceiptAttachmentFile(IFormFile? file, ExpenseAttachmentSettings settings)
    {
        if (file is null || file.Length == 0)
        {
            return EndpointSupport.ValidationProblem("file", "Upload a receipt file.");
        }

        if (file.Length > settings.MaxFileSizeBytes)
        {
            return EndpointSupport.ValidationProblem("file", $"Receipt files must be {settings.MaxFileSizeBytes / 1024 / 1024} MB or smaller.");
        }

        if (!settings.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return EndpointSupport.ValidationProblem("file", "Receipt files must be PDF, JPG, PNG, WebP or HEIC.");
        }

        return null;
    }

    public static IResult? ValidateExternalResourceAttachmentFile(IFormFile? file, ExpenseAttachmentSettings settings)
    {
        if (file is null || file.Length == 0)
        {
            return EndpointSupport.ValidationProblem("file", "Upload an attachment file.");
        }

        if (file.Length > settings.MaxFileSizeBytes)
        {
            return EndpointSupport.ValidationProblem("file", $"Attachment files must be {settings.MaxFileSizeBytes / 1024 / 1024} MB or smaller.");
        }

        return null;
    }

    public static string BuildAttachmentStorageKey(Guid? userId, Guid gigId, Guid expenseId, Guid attachmentId)
    {
        var owner = userId?.ToString("N") ?? "system";
        return $"users/{owner}/gigs/{gigId:N}/expenses/{expenseId:N}/attachments/{attachmentId:N}";
    }

    public static string BuildExternalResourceAttachmentStorageKey(Guid? userId, Guid gigId, Guid resourceId, Guid attachmentId)
    {
        var owner = userId?.ToString("N") ?? "system";
        return $"users/{owner}/gigs/{gigId:N}/external-resources/{resourceId:N}/attachments/{attachmentId:N}";
    }
}
