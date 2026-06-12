using System.Text.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class GigImportEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapGigImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/gig-imports")
            .WithTags("GigImports")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser);

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var batches = await db.GigImportBatches
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(batch => batch.Drafts)
                .OrderByDescending(batch => batch.CreatedAtUtc)
                .ToListAsync();

            return Results.Ok(batches.Select(ToSummary));
        });

        group.MapGet("/{batchId:guid}", async (
            Guid batchId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IGigImportDuplicateDetectionService duplicateDetectionService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var batch = await db.GigImportBatches
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Drafts)
                .FirstOrDefaultAsync(value => value.Id == batchId);

            if (batch is null)
            {
                return Results.NotFound();
            }

            var duplicateWarnings = await duplicateDetectionService.FindWarningsAsync(
                userId,
                batch,
                batch.Drafts.ToList());

            return Results.Ok(ToDetail(batch, duplicateWarnings));
        });

        group.MapPut("/{batchId:guid}/drafts/{draftId:guid}", async (
            Guid batchId,
            Guid draftId,
            GigImportDraftUpdateRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IGigImportDuplicateDetectionService duplicateDetectionService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var draft = await db.GigImportDrafts
                .Include(value => value.Batch)
                .FirstOrDefaultAsync(value => value.Id == draftId && value.BatchId == batchId);

            if (draft?.Batch is null || !CanSee(draft.Batch, userId))
            {
                return Results.NotFound();
            }

            if (draft.Status == GigImportDraftStatus.Committed)
            {
                return EndpointSupport.ValidationProblem("status", "Committed import rows cannot be edited.");
            }

            if (draft.Batch.Status != GigImportBatchStatus.Draft)
            {
                return EndpointSupport.ValidationProblem("batch", "Only draft import batches can be edited.");
            }

            if (request.ProposedClientId.HasValue && !await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == request.ProposedClientId.Value))
            {
                return EndpointSupport.ValidationProblem("proposedClientId", "Client does not exist.");
            }

            if (!TryApplyDraftStatus(request.Status, draft, out var statusError))
            {
                return statusError;
            }

            draft.ProposedClientId = request.ProposedClientId;
            draft.ProposedClientName = Normalize(request.ClientName);
            draft.ProposedContactName = Normalize(request.ContactName);
            draft.ProposedContactEmail = Normalize(request.ContactEmail);
            draft.ProposedProjectName = Normalize(request.ProjectName);
            draft.ProposedTitle = Normalize(request.Title);
            draft.ProposedDate = request.Date;
            draft.ProposedArrivalTime = request.ArrivalTime;
            draft.ProposedRehearsalStartTime = request.RehearsalStartTime;
            draft.ProposedRehearsalEndTime = request.RehearsalEndTime;
            draft.ProposedShowStartTime = request.ShowStartTime;
            draft.ProposedShowEndTime = request.ShowEndTime;
            draft.ProposedVenueName = Normalize(request.VenueName);
            draft.ProposedVenueAddress = Normalize(request.VenueAddress);
            draft.ProposedVenuePostcode = Normalize(request.Postcode);
            draft.ProposedFee = request.Fee;
            draft.ProposedPerDiem = request.PerDiem;
            draft.ProposedNotes = Normalize(request.Notes);
            draft.AccommodationNotes = Normalize(request.AccommodationNotes);
            draft.TravelNotes = Normalize(request.TravelNotes);
            draft.SourceReference = Normalize(request.SourceReference);
            draft.Confidence = request.Confidence ?? draft.Confidence;
            draft.WarningsJson = JsonSerializer.Serialize(
                PersistedWarnings(request.Warnings ?? ReadWarnings(draft.WarningsJson)),
                JsonOptions);

            await db.SaveChangesAsync();

            var duplicateWarnings = await duplicateDetectionService.FindWarningsAsync(
                userId,
                draft.Batch,
                [draft]);

            return Results.Ok(ToDraftDetail(draft, duplicateWarnings));
        });

        group.MapPost("/{batchId:guid}/commit", async (
            Guid batchId,
            GigImportCommitRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IWorkspaceEventPublisher workspaceEventPublisher,
            IGigImportDuplicateDetectionService duplicateDetectionService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var batch = await db.GigImportBatches
                .WhereVisibleTo(userId)
                .Include(value => value.Drafts)
                .FirstOrDefaultAsync(value => value.Id == batchId);

            if (batch is null)
            {
                return Results.NotFound();
            }

            if (batch.Status != GigImportBatchStatus.Draft)
            {
                return EndpointSupport.ValidationProblem("batch", "Only draft import batches can be committed.");
            }

            var requestedDraftIds = request.DraftIds?.Where(value => value != Guid.Empty).Distinct().ToHashSet();
            var reviewedDraftCount = batch.Drafts.Count(draft => draft.Status is GigImportDraftStatus.Accepted or GigImportDraftStatus.Rejected);
            var rejectedDrafts = batch.Drafts
                .Where(draft => draft.Status == GigImportDraftStatus.Rejected)
                .ToList();
            var draftsToCommit = batch.Drafts
                .Where(draft =>
                    draft.Status != GigImportDraftStatus.Rejected &&
                    draft.Status != GigImportDraftStatus.Committed &&
                    (request.CommitAccepted
                        ? draft.Status == GigImportDraftStatus.Accepted
                        : requestedDraftIds?.Contains(draft.Id) == true))
                .OrderBy(draft => draft.ProposedDate ?? DateOnly.MaxValue)
                .ThenBy(draft => draft.ProposedTitle)
                .ToList();

            if (draftsToCommit.Count == 0)
            {
                if (reviewedDraftCount == 0)
                {
                    return EndpointSupport.ValidationProblem("draftIds", "Accept or reject at least one row before committing decisions.");
                }

                DeleteRejectedDrafts(db, batch, rejectedDrafts);
                UpdateBatchStatusAfterCommittedDecisions(batch);

                await db.SaveChangesAsync();

                var duplicateWarnings = await duplicateDetectionService.FindWarningsAsync(
                    userId,
                    batch,
                    batch.Drafts.ToList());

                return Results.Ok(new GigImportCommitResult(
                    0,
                    [],
                    ToDetail(batch, duplicateWarnings)));
            }

            var errors = await ValidateCommitDraftsAsync(db, userId, draftsToCommit);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var gigs = draftsToCommit.Select(draft => BuildGig(batch, draft, userId)).ToList();
            db.Gigs.AddRange(gigs);

            foreach (var draft in draftsToCommit)
            {
                draft.Status = GigImportDraftStatus.Committed;
            }

            DeleteRejectedDrafts(db, batch, rejectedDrafts);
            UpdateBatchStatusAfterCommittedDecisions(batch);

            await db.SaveChangesAsync();
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "created", null, DateTimeOffset.UtcNow));

            var remainingDuplicateWarnings = await duplicateDetectionService.FindWarningsAsync(
                userId,
                batch,
                batch.Drafts.ToList());

            return Results.Ok(new GigImportCommitResult(
                gigs.Count,
                gigs.Select(gig => gig.Id).ToList(),
                ToDetail(batch, remainingDuplicateWarnings)));
        });

        return app;
    }

    private static void DeleteRejectedDrafts(AppDbContext db, GigImportBatch batch, IReadOnlyList<GigImportDraft> rejectedDrafts)
    {
        if (rejectedDrafts.Count == 0)
        {
            return;
        }

        db.GigImportDrafts.RemoveRange(rejectedDrafts);
        foreach (var draft in rejectedDrafts)
        {
            batch.Drafts.Remove(draft);
        }
    }

    private static void UpdateBatchStatusAfterCommittedDecisions(GigImportBatch batch)
    {
        if (batch.Drafts.Count == 0)
        {
            batch.Status = GigImportBatchStatus.Abandoned;
            return;
        }

        if (batch.Drafts.All(draft => draft.Status == GigImportDraftStatus.Committed))
        {
            batch.Status = GigImportBatchStatus.Committed;
        }
    }

    private static async Task<Dictionary<string, string[]>> ValidateCommitDraftsAsync(
        AppDbContext db,
        Guid? userId,
        IReadOnlyList<GigImportDraft> drafts)
    {
        var errors = new Dictionary<string, string[]>();
        var clientIds = drafts
            .Select(draft => draft.ProposedClientId)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .ToList();
        var visibleClientIds = await db.Clients
            .WhereVisibleTo(userId)
            .Where(client => clientIds.Contains(client.Id))
            .Select(client => client.Id)
            .ToListAsync();
        var visibleClientIdSet = visibleClientIds.ToHashSet();

        foreach (var draft in drafts)
        {
            var key = $"drafts.{draft.Id}";
            var draftErrors = new List<string>();

            if (!draft.ProposedClientId.HasValue)
            {
                draftErrors.Add("Client is required.");
            }
            else if (!visibleClientIdSet.Contains(draft.ProposedClientId.Value))
            {
                draftErrors.Add("Client does not exist.");
            }

            if (string.IsNullOrWhiteSpace(draft.ProposedTitle))
            {
                draftErrors.Add("Title is required.");
            }

            if (!draft.ProposedDate.HasValue)
            {
                draftErrors.Add("Date is required.");
            }

            if (string.IsNullOrWhiteSpace(BuildVenue(draft)))
            {
                draftErrors.Add("Location or venue is required.");
            }

            if (draft.ProposedFee < 0)
            {
                draftErrors.Add("Fee cannot be negative.");
            }

            var alreadyCommitted = await db.Gigs.AnyAsync(gig => gig.SourceImportDraftId == draft.Id);
            if (alreadyCommitted)
            {
                draftErrors.Add("This row has already been committed.");
            }

            if (draftErrors.Count > 0)
            {
                errors[key] = draftErrors.ToArray();
            }
        }

        return errors;
    }

    private static Gig BuildGig(GigImportBatch batch, GigImportDraft draft, Guid? userId)
    {
        var gig = new Gig
        {
            Id = Guid.NewGuid(),
            ClientId = draft.ProposedClientId!.Value,
            Title = draft.ProposedTitle!.Trim(),
            Date = draft.ProposedDate!.Value,
            Venue = BuildVenue(draft),
            Fee = draft.ProposedFee ?? 0m,
            TravelMiles = 0m,
            PassengerCount = null,
            Notes = BuildNotes(draft),
            WasDriving = false,
            Status = GigStatus.Confirmed,
            SourceImportBatchId = batch.Id,
            SourceImportDraftId = draft.Id,
            Expenses = BuildExpenses(draft),
        };

        EndpointSupport.StampCreate(gig, userId);
        return gig;
    }

    private static List<GigExpense> BuildExpenses(GigImportDraft draft)
    {
        if (!draft.ProposedPerDiem.HasValue || draft.ProposedPerDiem.Value <= 0)
        {
            return [];
        }

        return
        [
            new GigExpense
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                Description = "Per diem",
                Amount = draft.ProposedPerDiem.Value,
            }
        ];
    }

    private static string? BuildNotes(GigImportDraft draft)
    {
        var lines = new[]
        {
            draft.ProposedNotes,
            draft.AccommodationNotes is null ? null : $"Accommodation: {draft.AccommodationNotes}",
            draft.TravelNotes is null ? null : $"Travel: {draft.TravelNotes}",
            draft.SourceReference is null ? null : $"Source: {draft.SourceReference}",
        }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string BuildVenue(GigImportDraft draft)
    {
        return string.Join(", ", new[]
        {
            draft.ProposedVenueName,
            draft.ProposedVenueAddress,
            draft.ProposedVenuePostcode,
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
    }

    private static bool TryApplyDraftStatus(string? requestedStatus, GigImportDraft draft, out IResult error)
    {
        error = Results.Empty;
        if (string.IsNullOrWhiteSpace(requestedStatus))
        {
            return true;
        }

        if (!Enum.TryParse<GigImportDraftStatus>(requestedStatus, ignoreCase: true, out var status) ||
            status == GigImportDraftStatus.Committed)
        {
            error = EndpointSupport.ValidationProblem("status", "Status is invalid.");
            return false;
        }

        draft.Status = status;
        return true;
    }

    private static GigImportBatchSummaryDto ToSummary(GigImportBatch batch)
    {
        return new GigImportBatchSummaryDto(
            batch.Id,
            batch.SourceName,
            batch.SourceFingerprint,
            batch.Status.ToString(),
            batch.CreatedAtUtc,
            batch.Notes,
            batch.Drafts.Count,
            batch.Drafts.Count(draft => draft.Status == GigImportDraftStatus.Pending),
            batch.Drafts.Count(draft => draft.Status == GigImportDraftStatus.Accepted),
            batch.Drafts.Count(draft => draft.Status == GigImportDraftStatus.Rejected),
            batch.Drafts.Count(draft => draft.Status == GigImportDraftStatus.Committed),
            batch.Drafts.Count(draft => draft.Confidence == GigImportDraftConfidence.Low),
            batch.Drafts.Count(draft => draft.Confidence == GigImportDraftConfidence.Medium),
            batch.Drafts.Count(draft => draft.Confidence == GigImportDraftConfidence.High));
    }

    private static GigImportBatchDetailDto ToDetail(
        GigImportBatch batch,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? duplicateWarnings = null)
    {
        return new GigImportBatchDetailDto(
            ToSummary(batch),
            batch.Drafts
                .OrderBy(draft => IsHandledForOrdering(batch, draft))
                .ThenBy(draft => draft.ProposedDate ?? DateOnly.MaxValue)
                .ThenBy(draft => draft.ProposedTitle)
                .Select(draft => ToDraftDetail(draft, duplicateWarnings))
                .ToList());
    }

    private static bool IsHandledForOrdering(GigImportBatch batch, GigImportDraft draft)
    {
        return draft.Status == GigImportDraftStatus.Committed ||
            (batch.Status != GigImportBatchStatus.Draft && draft.Status == GigImportDraftStatus.Rejected);
    }

    private static GigImportDraftDetailDto ToDraftDetail(
        GigImportDraft draft,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? duplicateWarnings = null)
    {
        var warnings = CombinedWarnings(draft, duplicateWarnings);

        return new GigImportDraftDetailDto(
            draft.Id,
            draft.BatchId,
            draft.ProposedClientId,
            draft.ProposedClientName,
            draft.ProposedContactName,
            draft.ProposedContactEmail,
            draft.ProposedProjectName,
            draft.ProposedTitle,
            draft.ProposedDate,
            draft.ProposedArrivalTime,
            draft.ProposedRehearsalStartTime,
            draft.ProposedRehearsalEndTime,
            draft.ProposedShowStartTime,
            draft.ProposedShowEndTime,
            draft.ProposedVenueName,
            draft.ProposedVenueAddress,
            draft.ProposedVenuePostcode,
            draft.ProposedFee,
            draft.ProposedPerDiem,
            draft.ProposedNotes,
            draft.AccommodationNotes,
            draft.TravelNotes,
            draft.SourceReference,
            draft.Confidence.ToString(),
            warnings,
            draft.Status.ToString(),
            MissingFields(draft));
    }

    private static IReadOnlyList<string> CombinedWarnings(
        GigImportDraft draft,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? duplicateWarnings)
    {
        var warnings = PersistedWarnings(ReadWarnings(draft.WarningsJson)).ToList();
        if (duplicateWarnings?.TryGetValue(draft.Id, out var generatedWarnings) == true)
        {
            foreach (var warning in generatedWarnings)
            {
                if (!warnings.Contains(warning, StringComparer.Ordinal))
                {
                    warnings.Add(warning);
                }
            }
        }

        return warnings;
    }

    private static IReadOnlyList<string> MissingFields(GigImportDraft draft)
    {
        var fields = new List<string>();
        if (!draft.ProposedClientId.HasValue)
        {
            fields.Add("client");
        }

        if (string.IsNullOrWhiteSpace(draft.ProposedTitle))
        {
            fields.Add("title");
        }

        if (!draft.ProposedDate.HasValue)
        {
            fields.Add("date");
        }

        if (string.IsNullOrWhiteSpace(BuildVenue(draft)))
        {
            fields.Add("venue");
        }

        return fields;
    }

    private static IReadOnlyList<string> ReadWarnings(string warningsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(warningsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> PersistedWarnings(IReadOnlyList<string> warnings)
    {
        return warnings
            .Where(warning => !warning.StartsWith(GigImportDuplicateDetectionService.GeneratedWarningPrefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Take(25)
            .ToList();
    }

    private static bool CanSee(GigImportBatch batch, Guid? userId)
    {
        return batch.CreatedByUserId is null || batch.CreatedByUserId == userId;
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}

public sealed record GigImportDraftUpdateRequest(
    Guid? ProposedClientId,
    string? ClientName,
    string? ContactName,
    string? ContactEmail,
    string? ProjectName,
    string? Title,
    DateOnly? Date,
    TimeOnly? ArrivalTime,
    TimeOnly? RehearsalStartTime,
    TimeOnly? RehearsalEndTime,
    TimeOnly? ShowStartTime,
    TimeOnly? ShowEndTime,
    string? VenueName,
    string? VenueAddress,
    string? Postcode,
    decimal? Fee,
    decimal? PerDiem,
    string? Notes,
    string? AccommodationNotes,
    string? TravelNotes,
    string? SourceReference,
    GigImportDraftConfidence? Confidence,
    IReadOnlyList<string>? Warnings,
    string? Status);

public sealed record GigImportCommitRequest(IReadOnlyList<Guid>? DraftIds, bool CommitAccepted);
public sealed record GigImportCommitResult(int CreatedCount, IReadOnlyList<Guid> GigIds, GigImportBatchDetailDto Batch);
public sealed record GigImportBatchSummaryDto(
    Guid BatchId,
    string SourceName,
    string? SourceFingerprint,
    string Status,
    DateTimeOffset CreatedAtUtc,
    string? Notes,
    int DraftCount,
    int PendingCount,
    int AcceptedCount,
    int RejectedCount,
    int CommittedCount,
    int LowConfidenceCount,
    int MediumConfidenceCount,
    int HighConfidenceCount);
public sealed record GigImportBatchDetailDto(GigImportBatchSummaryDto Batch, IReadOnlyList<GigImportDraftDetailDto> Drafts);
public sealed record GigImportDraftDetailDto(
    Guid DraftId,
    Guid BatchId,
    Guid? ProposedClientId,
    string? ClientName,
    string? ContactName,
    string? ContactEmail,
    string? ProjectName,
    string? Title,
    DateOnly? Date,
    TimeOnly? ArrivalTime,
    TimeOnly? RehearsalStartTime,
    TimeOnly? RehearsalEndTime,
    TimeOnly? ShowStartTime,
    TimeOnly? ShowEndTime,
    string? VenueName,
    string? VenueAddress,
    string? Postcode,
    decimal? Fee,
    decimal? PerDiem,
    string? Notes,
    string? AccommodationNotes,
    string? TravelNotes,
    string? SourceReference,
    string Confidence,
    IReadOnlyList<string> Warnings,
    string Status,
    IReadOnlyList<string> MissingFields);
