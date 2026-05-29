using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigCalendarSyncState
{
    public Guid Id { get; set; }
    public Guid? GigId { get; set; }
    public Guid UserId { get; set; }
    public CalendarProvider Provider { get; set; } = CalendarProvider.GoogleCalendar;
    public string? ProviderCalendarId { get; set; }
    public string? ProviderEventId { get; set; }
    public string? LastSyncHash { get; set; }
    public DateTimeOffset? LastSyncedAtUtc { get; set; }
    public string? LastSyncError { get; set; }
    public DateTimeOffset? LastSyncAttemptedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
}
