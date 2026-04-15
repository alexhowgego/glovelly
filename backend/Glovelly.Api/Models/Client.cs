using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class Client
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Address BillingAddress { get; set; } = new();
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    [JsonIgnore]
    public ICollection<Gig> Gigs { get; set; } = new List<Gig>();
    [JsonIgnore]
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    [JsonIgnore]
    public User? CreatedByUser { get; set; }
    [JsonIgnore]
    public User? UpdatedByUser { get; set; }
}
