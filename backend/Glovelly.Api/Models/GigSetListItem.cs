using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigSetListItem
{
    public Guid Id { get; set; }
    public Guid GigSetListImportId { get; set; }
    public int SortOrder { get; set; }
    public int SourceRowNumber { get; set; }
    public GigSetListItemKind Kind { get; set; } = GigSetListItemKind.Song;
    public bool Include { get; set; } = true;
    public string? Section { get; set; }
    public string? PadNumber { get; set; }
    public string? Key { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string RawCellsJson { get; set; } = "[]";
    public GigSetListItemConfidence Confidence { get; set; } = GigSetListItemConfidence.Medium;
    public Guid? ForScoreLibrarySnapshotId { get; set; }
    public Guid? ForScoreChartId { get; set; }
    public string? ForScoreChartTitle { get; set; }
    public string? ForScoreChartFilePath { get; set; }
    public ForScoreMappingStatus ForScoreMappingStatus { get; set; } = ForScoreMappingStatus.Unmapped;
    public ForScoreMappingConfidence ForScoreMappingConfidence { get; set; } = ForScoreMappingConfidence.None;
    public DateTimeOffset? ForScoreMappingUpdatedAtUtc { get; set; }

    [JsonIgnore]
    public GigSetListImport? Import { get; set; }

    [JsonIgnore]
    public ForScoreLibrarySnapshot? ForScoreLibrarySnapshot { get; set; }

    [JsonIgnore]
    public ForScoreChart? ForScoreChart { get; set; }
}
