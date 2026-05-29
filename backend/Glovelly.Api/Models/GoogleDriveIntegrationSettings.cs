using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GoogleDriveIntegrationSettings
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GoogleConnectionId { get; set; }
    public string? InvoiceUploadFolderId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    [JsonIgnore]
    public GoogleConnection? GoogleConnection { get; set; }
}
