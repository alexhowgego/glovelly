using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigExternalResource
{
    public Guid Id { get; set; }
    public Guid GigId { get; set; }
    public GigExternalResourceType ResourceType { get; set; } = GigExternalResourceType.Url;
    public GigExternalResourcePurpose Purpose { get; set; } = GigExternalResourcePurpose.Other;
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<GigExternalResourceAttachment> Attachments { get; set; } = new List<GigExternalResourceAttachment>();

    [JsonIgnore]
    public Gig? Gig { get; set; }
}
