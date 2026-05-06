using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigExpense
{
    public Guid Id { get; set; }
    public Guid GigId { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public ICollection<ExpenseAttachment> Attachments { get; set; } = new List<ExpenseAttachment>();

    [JsonIgnore]
    public Gig? Gig { get; set; }
}
