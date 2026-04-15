using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class Gig
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Venue { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public decimal TravelMiles { get; set; }
    public string? Notes { get; set; }
    public bool Invoiced { get; set; }

    [JsonIgnore]
    public Client? Client { get; set; }
}
