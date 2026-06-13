using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class GigReceiptEndpoints
{
    public static RouteGroupBuilder MapGigReceiptEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/receipt-drafts", async (
            HttpRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            IWorkspaceEventPublisher workspaceEventPublisher,
            IOptions<ExpenseAttachmentSettings> attachmentOptions,
            IOptions<QuickReceiptCaptureSettings> quickReceiptOptions,
            TimeProvider timeProvider) =>
        {
            if (!request.HasFormContentType)
            {
                return EndpointSupport.ValidationProblem("file", "Upload a receipt file.");
            }

            var userId = currentUserAccessor.TryGetUserId(user);
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            var validation = GigEndpointSupport.ValidateAttachmentFile(file, attachmentOptions.Value);
            if (validation is not null)
            {
                return validation;
            }

            var gigId = TryReadGigId(form);
            var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            var settings = NormalizeQuickReceiptSettings(quickReceiptOptions.Value);
            var candidates = await FindReceiptCandidatesAsync(
                db,
                userId,
                today,
                settings.CandidateCount,
                settings.AutoAttachWindowDays);
            Gig? gig;
            if (gigId.HasValue)
            {
                gig = await db.Gigs
                    .WhereVisibleTo(userId)
                    .Include(value => value.Expenses)
                    .FirstOrDefaultAsync(value => value.Id == gigId.Value);

                if (gig is null)
                {
                    return EndpointSupport.ValidationProblem("gigId", "Gig does not exist.");
                }
            }
            else
            {
                var nearestCandidate = candidates.FirstOrDefault();
                if (nearestCandidate is null)
                {
                    return Results.Conflict(new
                    {
                        message = $"No gig was within {settings.AutoAttachWindowDays} days. Choose a gig before saving this receipt draft.",
                        candidates = candidates.Select(candidate => ToReceiptGigCandidate(candidate, nearestCandidate?.Id)),
                        autoAttachWindowDays = settings.AutoAttachWindowDays,
                    });
                }

                gig = await db.Gigs
                    .WhereVisibleTo(userId)
                    .Include(value => value.Expenses)
                    .FirstAsync(value => value.Id == nearestCandidate.Id);
            }

            var expense = new GigExpense
            {
                Id = Guid.NewGuid(),
                GigId = gig.Id,
                SortOrder = gig.Expenses.Count == 0 ? 1 : gig.Expenses.Max(value => value.SortOrder) + 1,
                Description = "Receipt draft",
                Amount = 0m,
            };

            var attachmentId = Guid.NewGuid();
            var storageKey = GigEndpointSupport.BuildAttachmentStorageKey(userId, gig.Id, expense.Id, attachmentId);
            await using var stream = file!.OpenReadStream();
            await attachmentStore.SaveAsync(storageKey, stream, file.ContentType);

            var displayFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(displayFileName))
            {
                displayFileName = "receipt";
            }

            expense.Attachments.Add(new ExpenseAttachment
            {
                Id = attachmentId,
                GigExpenseId = expense.Id,
                FileName = displayFileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StorageKey = storageKey,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            db.GigExpenses.Add(expense);
            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gig.Id, DateTimeOffset.UtcNow));

            var savedGig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                    .ThenInclude(value => value.Attachments)
                .FirstAsync(value => value.Id == gig.Id);

            return Results.Created($"/gigs/{savedGig.Id}", new
            {
                gig = savedGig,
                expenseId = expense.Id,
                attachmentId,
                inferredGig = !gigId.HasValue,
                candidates = candidates.Select(candidate => ToReceiptGigCandidate(candidate, gig.Id)),
                autoAttachWindowDays = settings.AutoAttachWindowDays,
                hasNearbyCandidates = candidates.Any(candidate =>
                    candidate.Id != gig.Id &&
                    candidate.DaysFromToday <= settings.AmbiguityWindowDays),
            });
        });

        group.MapPatch("/receipt-drafts/{expenseId:guid}", async (
            Guid expenseId,
            QuickReceiptDraftUpdateRequest update,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IWorkspaceEventPublisher workspaceEventPublisher,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var targetGig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(value => value.Id == update.GigId);

            if (targetGig is null)
            {
                return EndpointSupport.ValidationProblem("gigId", "Gig does not exist.");
            }

            var expense = await db.GigExpenses
                .Include(value => value.Attachments)
                .Include(value => value.Gig)
                .Where(value => value.Id == expenseId)
                .Where(value => value.Gig != null
                    && (value.Gig.CreatedByUserId == null || value.Gig.CreatedByUserId == userId))
                .FirstOrDefaultAsync();

            if (expense is null)
            {
                return Results.NotFound();
            }

            var description = update.Description?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(description))
            {
                return EndpointSupport.ValidationProblem("description", "Expense description is required.");
            }

            if (update.Amount < 0)
            {
                return EndpointSupport.ValidationProblem("amount", "Expense amount cannot be negative.");
            }

            var previousGigId = expense.GigId;
            var moved = previousGigId != targetGig.Id;

            expense.Description = description;
            expense.Amount = update.Amount;
            if (moved)
            {
                expense.GigId = targetGig.Id;
                expense.Gig = targetGig;
                expense.SortOrder = targetGig.Expenses.Count == 0
                    ? 1
                    : targetGig.Expenses.Max(value => value.SortOrder) + 1;
                targetGig.Expenses.Add(expense);
            }

            if (expense.Gig is not null)
            {
                EndpointSupport.StampUpdate(expense.Gig, userId);
            }

            EndpointSupport.StampUpdate(targetGig, userId);
            await db.SaveChangesAsync();

            var affectedGigIds = moved
                ? new[] { previousGigId, targetGig.Id }
                : new[] { targetGig.Id };

            var affectedGigs = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                .Where(value => affectedGigIds.Contains(value.Id))
                .ToListAsync();

            foreach (var affectedGig in affectedGigs)
            {
                await invoiceWorkflowService.SyncGeneratedInvoiceLinesForGigAsync(affectedGig, userId);
            }

            await db.SaveChangesAsync();
            foreach (var affectedGigId in affectedGigIds)
            {
                await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", affectedGigId, DateTimeOffset.UtcNow));
            }

            var savedGigs = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Expenses)
                    .ThenInclude(value => value.Attachments)
                .Where(value => affectedGigIds.Contains(value.Id))
                .ToListAsync();

            var savedTargetGig = savedGigs.First(value => value.Id == targetGig.Id);
            var previousGig = moved
                ? savedGigs.FirstOrDefault(value => value.Id == previousGigId)
                : null;

            return Results.Ok(new
            {
                gig = savedTargetGig,
                previousGig,
                expenseId,
                moved,
            });
        });

        return group;
    }

    private static Guid? TryReadGigId(IFormCollection form)
    {
        var rawValue = form["gigId"].FirstOrDefault();
        return Guid.TryParse(rawValue, out var gigId) && gigId != Guid.Empty ? gigId : null;
    }

    private static async Task<List<ReceiptGigCandidate>> FindReceiptCandidatesAsync(
        AppDbContext db,
        Guid? userId,
        DateOnly today,
        int candidateCount,
        int autoAttachWindowDays)
    {
        var gigs = await db.Gigs
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .Where(value => value.Status != GigStatus.Cancelled)
            .ToListAsync();

        return gigs
            .Select(gig => new ReceiptGigCandidate(
                gig.Id,
                gig.ClientId,
                gig.Title,
                gig.Date,
                gig.Venue,
                gig.Status,
                Math.Abs(gig.Date.DayNumber - today.DayNumber)))
            .Where(candidate => candidate.DaysFromToday <= autoAttachWindowDays)
            .OrderBy(candidate => candidate.DaysFromToday)
            .ThenBy(candidate => candidate.Date)
            .ThenBy(candidate => candidate.Title)
            .Take(candidateCount)
            .ToList();
    }

    private static QuickReceiptCaptureSettings NormalizeQuickReceiptSettings(QuickReceiptCaptureSettings settings)
    {
        return new QuickReceiptCaptureSettings
        {
            CandidateCount = Math.Clamp(settings.CandidateCount, 1, 20),
            AutoAttachWindowDays = Math.Clamp(settings.AutoAttachWindowDays, 0, 365),
            AmbiguityWindowDays = Math.Clamp(settings.AmbiguityWindowDays, 0, 365),
        };
    }

    private static object ToReceiptGigCandidate(ReceiptGigCandidate candidate, Guid? selectedGigId)
    {
        return new
        {
            candidate.Id,
            candidate.ClientId,
            candidate.Title,
            candidate.Date,
            candidate.Venue,
            candidate.Status,
            candidate.DaysFromToday,
            IsSelected = candidate.Id == selectedGigId,
        };
    }

    private sealed record QuickReceiptDraftUpdateRequest(Guid GigId, string Description, decimal Amount);

    private sealed record ReceiptGigCandidate(
        Guid Id,
        Guid ClientId,
        string Title,
        DateOnly Date,
        string Venue,
        GigStatus Status,
        int DaysFromToday);
}
