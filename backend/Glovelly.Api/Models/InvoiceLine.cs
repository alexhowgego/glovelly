using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class InvoiceLine
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public int SortOrder { get; set; }
    public InvoiceLineType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid? GigId { get; set; }
    public string? CalculationNotes { get; set; }
    public bool IsSystemGenerated { get; set; }

    [JsonIgnore]
    public Invoice? Invoice { get; set; }
    [JsonIgnore]
    public Gig? Gig { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitPrice;
}
