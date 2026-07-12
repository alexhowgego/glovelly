using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class SetListChartMatchJob
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GigId { get; set; }
    public SetListChartMatchJobStatus Status { get; set; } = SetListChartMatchJobStatus.Pending;
    public string InputJson { get; set; } = "[]";
    public string? ResultJson { get; set; }
    public string? SafeErrorMessage { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    [JsonIgnore]
    public Gig? Gig { get; set; }
}
