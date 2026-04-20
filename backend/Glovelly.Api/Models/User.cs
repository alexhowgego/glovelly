using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace Glovelly.Api.Models;

public sealed class User
{
    public Guid Id { get; set; }
    public string? GoogleSubject { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public decimal? MileageRate { get; set; }
    public decimal? PassengerMileageRate { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    [NotMapped]
    public bool IsEnrolled => GoogleSubject is not null;

    [JsonIgnore]
    public ICollection<Client> ClientsCreated { get; set; } = new List<Client>();
    [JsonIgnore]
    public ICollection<Client> ClientsUpdated { get; set; } = new List<Client>();
    [JsonIgnore]
    public ICollection<Gig> GigsCreated { get; set; } = new List<Gig>();
    [JsonIgnore]
    public ICollection<Gig> GigsUpdated { get; set; } = new List<Gig>();
    [JsonIgnore]
    public ICollection<Invoice> InvoicesCreated { get; set; } = new List<Invoice>();
    [JsonIgnore]
    public ICollection<Invoice> InvoicesUpdated { get; set; } = new List<Invoice>();
}
