using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigImportBatch
{
    public Guid Id { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? SourceFingerprint { get; set; }
    public GigImportBatchStatus Status { get; set; } = GigImportBatchStatus.Draft;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? Notes { get; set; }

    public ICollection<GigImportDraft> Drafts { get; set; } = new List<GigImportDraft>();

    [JsonIgnore]
    public User? CreatedByUser { get; set; }
}
