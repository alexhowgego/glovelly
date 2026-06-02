using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class CalendarSyncWorkItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? GigId { get; set; }
    public CalendarProvider Provider { get; set; } = CalendarProvider.GoogleCalendar;
    public CalendarSyncWorkItemReason Reason { get; set; }
    public CalendarSyncWorkItemStatus Status { get; set; } = CalendarSyncWorkItemStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public string? ProcessingOwnerId { get; set; }
    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }
    public DateTimeOffset? LastAttemptedAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? LastErrorType { get; set; }
    public string? LastErrorDetail { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

}
