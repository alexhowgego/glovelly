using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class SellerProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? SellerName { get; set; }
    public Address Address { get; set; } = new();
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AccountName { get; set; }
    public string? SortCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? PaymentReferenceNote { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
    [JsonIgnore]
    public User? CreatedByUser { get; set; }
    [JsonIgnore]
    public User? UpdatedByUser { get; set; }
}
