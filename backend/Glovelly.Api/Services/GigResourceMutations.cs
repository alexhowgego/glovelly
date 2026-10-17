using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

internal static class GigResourceMutations
{
    public static GigExternalResource Create(Guid id, Guid gigId, GigExternalResourceType type, GigExternalResourcePurpose purpose, string title, string? url, string? notes, bool primary, DateTimeOffset now) => new()
    {
        Id = id, GigId = gigId, ResourceType = type, Purpose = purpose, Title = title.Trim(),
        Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim(), Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        IsPrimary = primary, CreatedAt = now, UpdatedAt = now,
    };
    public static void ClearPrimary(IEnumerable<GigExternalResource> resources, GigExternalResourcePurpose purpose, Guid exceptId)
    {
        foreach (var resource in resources.Where(resource => resource.Purpose == purpose && resource.Id != exceptId)) resource.IsPrimary = false;
    }
}
