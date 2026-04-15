namespace Glovelly.Api.Models;

public sealed class Client
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Address BillingAddress { get; set; } = new();

    public ICollection<Gig> Gigs { get; set; } = new List<Gig>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
