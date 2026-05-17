using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class GigAttachmentEndpoints
{
    public static RouteGroupBuilder MapGigAttachmentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{gigId:guid}/expenses/{expenseId:guid}/attachments", async (
            Guid gigId,
            Guid expenseId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var expense = await GigEndpointSupport.FindVisibleExpenseAsync(db, userId, gigId, expenseId, asNoTracking: true);

            return expense is null
                ? Results.NotFound()
                : Results.Ok(expense.Attachments.OrderBy(attachment => attachment.CreatedAt));
        });

        group.MapPost("/{gigId:guid}/expenses/{expenseId:guid}/attachments", async (
            Guid gigId,
            Guid expenseId,
            HttpRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            IOptions<ExpenseAttachmentSettings> attachmentOptions) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["Upload a receipt file."]
                });
            }

            var userId = currentUserAccessor.TryGetUserId(user);
            var expense = await GigEndpointSupport.FindVisibleExpenseAsync(db, userId, gigId, expenseId, asNoTracking: false);
            if (expense is null)
            {
                return Results.NotFound();
            }

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            var validation = GigEndpointSupport.ValidateAttachmentFile(file, attachmentOptions.Value);
            if (validation is not null)
            {
                return validation;
            }

            var attachmentId = Guid.NewGuid();
            var storageKey = GigEndpointSupport.BuildAttachmentStorageKey(userId, gigId, expenseId, attachmentId);
            await using var stream = file!.OpenReadStream();
            await attachmentStore.SaveAsync(storageKey, stream, file.ContentType);

            var displayFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(displayFileName))
            {
                displayFileName = "receipt";
            }

            var attachment = new ExpenseAttachment
            {
                Id = attachmentId,
                GigExpenseId = expenseId,
                FileName = displayFileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StorageKey = storageKey,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.ExpenseAttachments.Add(attachment);
            await db.SaveChangesAsync();

            return Results.Created($"/gigs/{gigId}/expenses/{expenseId}/attachments/{attachment.Id}", attachment);
        });

        group.MapGet("/{gigId:guid}/expenses/{expenseId:guid}/attachments/{attachmentId:guid}", async (
            Guid gigId,
            Guid expenseId,
            Guid attachmentId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var attachment = await GigEndpointSupport.FindVisibleAttachmentAsync(db, userId, gigId, expenseId, attachmentId, asNoTracking: true);
            if (attachment is null)
            {
                return Results.NotFound();
            }

            try
            {
                var content = await attachmentStore.OpenReadAsync(attachment.StorageKey);
                return Results.File(
                    content.Content,
                    content.ContentType ?? attachment.ContentType,
                    attachment.FileName,
                    enableRangeProcessing: true);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapDelete("/{gigId:guid}/expenses/{expenseId:guid}/attachments/{attachmentId:guid}", async (
            Guid gigId,
            Guid expenseId,
            Guid attachmentId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var attachment = await GigEndpointSupport.FindVisibleAttachmentAsync(db, userId, gigId, expenseId, attachmentId, asNoTracking: false);
            if (attachment is null)
            {
                return Results.NotFound();
            }

            await attachmentStore.DeleteAsync(attachment.StorageKey);
            db.ExpenseAttachments.Remove(attachment);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return group;
    }
}
