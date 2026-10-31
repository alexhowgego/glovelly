using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigImportBatch
{
    public Guid Id { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? SourceFingerprint { get; set; }
    public Guid? SourceIntakeId { get; set; }
    public IntakeSourceType? SourceType { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceContentType { get; set; }
    [JsonIgnore]
    public string? SourceStorageKey { get; set; }
    public string? SourceEvidenceJson { get; set; }
    public GigImportBatchStatus Status { get; set; } = GigImportBatchStatus.Draft;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? Notes { get; set; }

    public ICollection<GigImportDraft> Drafts { get; set; } = new List<GigImportDraft>();

    [JsonIgnore]
    public User? CreatedByUser { get; set; }
}
