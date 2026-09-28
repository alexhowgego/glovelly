using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class SetListInterpretationJob
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GigId { get; set; }
    public Guid GigExternalResourceId { get; set; }
    public string SpreadsheetId { get; set; } = string.Empty;
    public string? WorksheetId { get; set; }
    public string WorksheetName { get; set; } = string.Empty;
    public SetListInterpretationJobStatus Status { get; set; }
    public string SourceGridJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? SafeErrorMessage { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? SourceGridExpiresAtUtc { get; set; }

    [JsonIgnore] public User? User { get; set; }
    [JsonIgnore] public Gig? Gig { get; set; }
}
