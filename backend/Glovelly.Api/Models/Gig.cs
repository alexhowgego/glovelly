using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class Gig
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Venue { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public decimal TravelMiles { get; set; }
    public string? Notes { get; set; }
    public bool WasDriving { get; set; }
    public GigStatus Status { get; set; } = GigStatus.Draft;
    public DateTimeOffset? InvoicedAt { get; set; }
    public bool IsInvoiced => InvoiceId.HasValue;

    [JsonIgnore]
    public Client? Client { get; set; }
    [JsonIgnore]
    public Invoice? Invoice { get; set; }
    [JsonIgnore]
    public User? CreatedByUser { get; set; }
    [JsonIgnore]
    public User? UpdatedByUser { get; set; }
}
