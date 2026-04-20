using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class Invoice
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTimeOffset? StatusUpdatedUtc { get; set; }
    public string? Description { get; set; }
    public byte[]? PdfBlob { get; set; }

    [JsonIgnore]
    public Client? Client { get; set; }
    [JsonIgnore]
    public User? CreatedByUser { get; set; }
    [JsonIgnore]
    public User? UpdatedByUser { get; set; }
    [JsonIgnore]
    public ICollection<Gig> Gigs { get; set; } = new List<Gig>();
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    [NotMapped]
    public decimal Total => Lines.Sum(line => line.LineTotal);
}
