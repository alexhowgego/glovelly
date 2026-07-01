using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class ForScoreChart
{
    public Guid Id { get; set; }
    public Guid ForScoreLibrarySnapshotId { get; set; }
    public int SortOrder { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;
    public string? Keywords { get; set; }
    public DateTimeOffset? AddedAt { get; set; }
    public int? PrintNumber { get; set; }
    public int? Version { get; set; }

    [JsonIgnore]
    public ForScoreLibrarySnapshot? Snapshot { get; set; }
}
