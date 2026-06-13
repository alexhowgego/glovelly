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
            var validation = GigEndpointSupport.ValidateAttachmentFile(file, attachmentOptions.Value);
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
            IExpenseAttachmentStore attachmentStore) =>
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
            catch (FileNotFoundException)
            {
                return Results.NotFound();
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

    private static void ClearPrimaryForPurpose(IEnumerable<GigExternalResource> resources, GigExternalResourcePurpose purpose, Guid exceptResourceId)
    {
        foreach (var resource in resources.Where(resource => resource.Purpose == purpose && resource.Id != exceptResourceId))
        {
            resource.IsPrimary = false;
        }
    }

    private static IResult? ValidateResourceRequest(GigExternalResourceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (!Enum.IsDefined(request.ResourceType))
        {
            errors["resourceType"] = ["Resource type is invalid."];
        }

        if (!Enum.IsDefined(request.Purpose))
        {
            errors["purpose"] = ["Purpose is invalid."];
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Title is required."];
        }

        if (!string.IsNullOrWhiteSpace(request.Url)
            && (!Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            errors["url"] = ["URL must be an absolute http or https URL."];
        }

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private sealed record GigExternalResourceRequest(
        GigExternalResourceType ResourceType,
        GigExternalResourcePurpose Purpose,
        string Title,
        string? Url,
        string? Notes,
        bool IsPrimary);
}
