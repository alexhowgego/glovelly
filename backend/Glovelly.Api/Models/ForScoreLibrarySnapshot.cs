using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class ForScoreLibrarySnapshot
{
    public Guid Id { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = "FourSb";
    public string? BackupVersion { get; set; }
    public bool IsActive { get; set; }
    public int ChartCount { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset ImportedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public ICollection<ForScoreChart> Charts { get; set; } = new List<ForScoreChart>();

    [JsonIgnore]
    public User? CreatedByUser { get; set; }
}
