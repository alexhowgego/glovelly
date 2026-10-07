using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class GigExternalResourceEndpoints
{
    public static RouteGroupBuilder MapGigExternalResourceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPatch("/external-resource-drafts/{resourceId:guid}", async (
            Guid resourceId,
            QuickExternalResourceDraftUpdateRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IWorkspaceEventPublisher workspaceEventPublisher) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var targetGig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(gig => gig.ExternalResources)
                .FirstOrDefaultAsync(gig => gig.Id == request.GigId);

            if (targetGig is null)
            {
                return EndpointSupport.ValidationProblem("gigId", "Gig does not exist.");
            }

            var resource = await db.GigExternalResources
                .Include(value => value.Attachments)
                .Include(value => value.Gig)
                .Where(value => value.Id == resourceId)
                .Where(value => value.Gig != null
                    && (value.Gig.CreatedByUserId == null || value.Gig.CreatedByUserId == userId))
                .FirstOrDefaultAsync();

            if (resource is null)
            {
                return Results.NotFound();
            }

            var validation = ValidateResourceRequest(new GigExternalResourceRequest(
                request.ResourceType,
                request.Purpose,
                request.Title,
                request.Url,
                request.Notes,
                request.IsPrimary));
            if (validation is not null)
            {
                return validation;
            }

            var previousGigId = resource.GigId;
            var moved = previousGigId != targetGig.Id;

            resource.GigId = targetGig.Id;
            resource.Gig = targetGig;
            resource.ResourceType = request.ResourceType;
            resource.Purpose = request.Purpose;
            resource.Title = request.Title.Trim();
            resource.Url = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim();
            resource.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            resource.IsPrimary = request.IsPrimary;
            resource.UpdatedAt = DateTimeOffset.UtcNow;

            if (resource.IsPrimary)
            {
                ClearPrimaryForPurpose(targetGig.ExternalResources, resource.Purpose, resource.Id);
            }

            if (resource.Gig is not null)
            {
                EndpointSupport.StampUpdate(resource.Gig, userId);
            }

            EndpointSupport.StampUpdate(targetGig, userId);
            await db.SaveChangesAsync();

            var affectedGigIds = moved
                ? new[] { previousGigId, targetGig.Id }
                : new[] { targetGig.Id };

            foreach (var affectedGigId in affectedGigIds)
            {
                await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", affectedGigId, DateTimeOffset.UtcNow));
            }

            var savedGigs = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .IncludeGigDetails()
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
                resourceId,
                moved,
            });
        });

        group.MapPost("/{gigId:guid}/external-resources", async (
            Guid gigId,
            GigExternalResourceRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IWorkspaceEventPublisher workspaceEventPublisher,
            IExpenseAttachmentStore attachmentStore) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(gig => gig.ExternalResources)
                .FirstOrDefaultAsync(gig => gig.Id == gigId);

            if (gig is null)
            {
                return Results.NotFound();
            }

            var validation = ValidateResourceRequest(request);
            if (validation is not null)
            {
                return validation;
            }

            var now = DateTimeOffset.UtcNow;
            var resource = new GigExternalResource
            {
                Id = Guid.NewGuid(),
                GigId = gigId,
                ResourceType = request.ResourceType,
                Purpose = request.Purpose,
                Title = request.Title.Trim(),
                Url = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                IsPrimary = request.IsPrimary,
                CreatedAt = now,
                UpdatedAt = now,
            };

            if (resource.IsPrimary)
            {
                ClearPrimaryForPurpose(gig.ExternalResources, resource.Purpose, resource.Id);
            }

            db.GigExternalResources.Add(resource);
            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gigId, DateTimeOffset.UtcNow));

            var savedGig = await LoadVisibleGigAsync(db, userId, gigId);
            return Results.Created($"/gigs/{gigId}/external-resources/{resource.Id}", savedGig);
        });

        group.MapPut("/{gigId:guid}/external-resources/{resourceId:guid}", async (
            Guid gigId,
            Guid resourceId,
            GigExternalResourceRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IWorkspaceEventPublisher workspaceEventPublisher,
            IExpenseAttachmentStore attachmentStore) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(gig => gig.ExternalResources)
                    .ThenInclude(resource => resource.Attachments)
                .FirstOrDefaultAsync(gig => gig.Id == gigId);

            if (gig is null)
            {
                return Results.NotFound();
            }

            var resource = gig.ExternalResources.FirstOrDefault(resource => resource.Id == resourceId);
            if (resource is null)
            {
                return Results.NotFound();
            }

            var validation = ValidateResourceRequest(request);
            if (validation is not null)
            {
                return validation;
            }

            resource.ResourceType = request.ResourceType;
            resource.Purpose = request.Purpose;
            resource.Title = request.Title.Trim();
            resource.Url = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim();
            resource.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            resource.IsPrimary = request.IsPrimary;
            resource.UpdatedAt = DateTimeOffset.UtcNow;

            if (resource.IsPrimary)
            {
                ClearPrimaryForPurpose(gig.ExternalResources, resource.Purpose, resource.Id);
            }

            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gigId, DateTimeOffset.UtcNow));

            var savedGig = await LoadVisibleGigAsync(db, userId, gigId);
            return Results.Ok(savedGig);
        });

        group.MapDelete("/{gigId:guid}/external-resources/{resourceId:guid}", async (
            Guid gigId,
            Guid resourceId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IWorkspaceEventPublisher workspaceEventPublisher,
            IExpenseAttachmentStore attachmentStore) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(gig => gig.ExternalResources)
                    .ThenInclude(resource => resource.Attachments)
                .FirstOrDefaultAsync(gig => gig.Id == gigId);

            if (gig is null)
            {
                return Results.NotFound();
            }

            var resource = gig.ExternalResources.FirstOrDefault(resource => resource.Id == resourceId);
            if (resource is null)
            {
                return Results.NotFound();
            }

            foreach (var attachment in resource.Attachments.ToList())
            {
                await attachmentStore.DeleteAsync(attachment.StorageKey);
                db.GigExternalResourceAttachments.Remove(attachment);
            }

            db.GigExternalResources.Remove(resource);
            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gigId, DateTimeOffset.UtcNow));

            var savedGig = await LoadVisibleGigAsync(db, userId, gigId);
            return Results.Ok(savedGig);
        });

        group.MapPost("/{gigId:guid}/external-resources/{resourceId:guid}/attachments", async (
            Guid gigId,
            Guid resourceId,
            HttpRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            IWorkspaceEventPublisher workspaceEventPublisher,
            IOptions<ExpenseAttachmentSettings> attachmentOptions) =>
        {
            if (!request.HasFormContentType)
            {
                return EndpointSupport.ValidationProblem("file", "Upload a resource file.");
            }

            var userId = currentUserAccessor.TryGetUserId(user);
            var resource = await GigEndpointSupport.FindVisibleExternalResourceAsync(db, userId, gigId, resourceId, asNoTracking: false);
            if (resource is null)
            {
                return Results.NotFound();
            }

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            var validation = GigEndpointSupport.ValidateExternalResourceAttachmentFile(file, attachmentOptions.Value);
            if (validation is not null)
            {
                return validation;
            }

            var attachmentId = Guid.NewGuid();
            var storageKey = GigEndpointSupport.BuildExternalResourceAttachmentStorageKey(userId, gigId, resourceId, attachmentId);
            await using var stream = file!.OpenReadStream();
            await attachmentStore.SaveAsync(storageKey, stream, file.ContentType);

            var displayFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(displayFileName))
            {
                displayFileName = "resource";
            }

            var attachment = new GigExternalResourceAttachment
            {
                Id = attachmentId,
                GigExternalResourceId = resourceId,
                FileName = displayFileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StorageKey = storageKey,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.GigExternalResourceAttachments.Add(attachment);
            resource.UpdatedAt = DateTimeOffset.UtcNow;
            if (resource.Gig is not null)
            {
                EndpointSupport.StampUpdate(resource.Gig, userId);
            }

            await db.SaveChangesAsync();
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gigId, DateTimeOffset.UtcNow));

            var savedGig = await LoadVisibleGigAsync(db, userId, gigId);
            return Results.Created($"/gigs/{gigId}/external-resources/{resourceId}/attachments/{attachment.Id}", savedGig);
        });

        group.MapGet("/{gigId:guid}/external-resources/{resourceId:guid}/attachments/{attachmentId:guid}", async (
            Guid gigId,
            Guid resourceId,
            Guid attachmentId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            ILogger<Program> logger) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var attachment = await GigEndpointSupport.FindVisibleExternalResourceAttachmentAsync(db, userId, gigId, resourceId, attachmentId, asNoTracking: true);
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
            catch (FileNotFoundException exception)
            {
                logger.LogWarning(
                    exception,
                    "Attachment object was not found: AttachmentId {AttachmentId}, StorageKey {StorageKey}.",
                    attachment.Id,
                    attachment.StorageKey);
                return Results.Problem(
                    detail: "This attachment file is no longer available.",
                    statusCode: StatusCodes.Status404NotFound);
            }
        });

        group.MapDelete("/{gigId:guid}/external-resources/{resourceId:guid}/attachments/{attachmentId:guid}", async (
            Guid gigId,
            Guid resourceId,
            Guid attachmentId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IWorkspaceEventPublisher workspaceEventPublisher,
            IExpenseAttachmentStore attachmentStore) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var attachment = await GigEndpointSupport.FindVisibleExternalResourceAttachmentAsync(db, userId, gigId, resourceId, attachmentId, asNoTracking: false);
            if (attachment is null)
            {
                return Results.NotFound();
            }

            await attachmentStore.DeleteAsync(attachment.StorageKey);
            if (attachment.Resource is not null)
            {
                attachment.Resource.UpdatedAt = DateTimeOffset.UtcNow;
            }

            if (attachment.Resource?.Gig is not null)
            {
                EndpointSupport.StampUpdate(attachment.Resource.Gig, userId);
            }

            db.GigExternalResourceAttachments.Remove(attachment);
            await db.SaveChangesAsync();
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gigId, DateTimeOffset.UtcNow));

            var savedGig = await LoadVisibleGigAsync(db, userId, gigId);
            return Results.Ok(savedGig);
        });

        return group;
    }

    private static Task<Gig?> LoadVisibleGigAsync(AppDbContext db, Guid? userId, Guid gigId)
    {
        return db.Gigs
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .IncludeGigDetails()
            .FirstOrDefaultAsync(gig => gig.Id == gigId);
    }

    internal static GigExternalResourceType InferResourceType(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return GigExternalResourceType.Url;
        }

        if (!string.Equals(uri.Host, "docs.google.com", StringComparison.OrdinalIgnoreCase))
        {
            return GigExternalResourceType.Url;
        }

        if (uri.AbsolutePath.StartsWith("/spreadsheets/", StringComparison.OrdinalIgnoreCase))
        {
            return GigExternalResourceType.GoogleSheet;
        }

        if (uri.AbsolutePath.StartsWith("/document/", StringComparison.OrdinalIgnoreCase))
        {
            return GigExternalResourceType.GoogleDoc;
        }

        return GigExternalResourceType.Url;
    }

    private static void ClearPrimaryForPurpose(IEnumerable<GigExternalResource> resources, GigExternalResourcePurpose purpose, Guid exceptResourceId)
    {
        foreach (var resource in resources.Where(resource => resource.Purpose == purpose && resource.Id != exceptResourceId))
        {
            resource.IsPrimary = false;
        }
    }

    private static IResult? ValidateResourceRequest(GigExternalResourceRequest request)
    {
        var errors = GigResourceValidation.Validate(request.ResourceType, request.Purpose, request.Title, request.Url, request.Notes);
        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private sealed record GigExternalResourceRequest(
        GigExternalResourceType ResourceType,
        GigExternalResourcePurpose Purpose,
        string Title,
        string? Url,
        string? Notes,
        bool IsPrimary);

    private sealed record QuickExternalResourceDraftUpdateRequest(
        Guid GigId,
        GigExternalResourceType ResourceType,
        GigExternalResourcePurpose Purpose,
        string Title,
        string? Url,
        string? Notes,
        bool IsPrimary);
}
