using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GoogleCalendarIntegrationSettings
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GoogleConnectionId { get; set; }
    public bool IsEnabled { get; set; }
    public string? GoogleCalendarId { get; set; }
    public string CalendarName { get; set; } = "Glovelly Gigs";
    public DateTimeOffset? LastSuccessfulSyncAtUtc { get; set; }
    public bool SyncAcceptedGigsOnly { get; set; } = true;
    public bool IncludeLocation { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DisconnectedAtUtc { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    [JsonIgnore]
    public GoogleConnection? GoogleConnection { get; set; }
}
