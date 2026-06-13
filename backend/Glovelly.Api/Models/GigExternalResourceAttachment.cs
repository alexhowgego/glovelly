using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigExternalResourceAttachment
{
    public Guid Id { get; set; }
    public Guid GigExternalResourceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    [JsonIgnore]
    public string StorageKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    [JsonIgnore]
    public GigExternalResource? Resource { get; set; }
}
