using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigSetListImport
{
    public Guid Id { get; set; }
    public Guid GigId { get; set; }
    public Guid? GigExternalResourceId { get; set; }
    public string SpreadsheetId { get; set; } = string.Empty;
    public string? WorksheetId { get; set; }
    public string WorksheetName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset ImportedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public ICollection<GigSetListItem> Items { get; set; } = new List<GigSetListItem>();

    [JsonIgnore]
    public Gig? Gig { get; set; }

    [JsonIgnore]
    public GigExternalResource? GigExternalResource { get; set; }
}
