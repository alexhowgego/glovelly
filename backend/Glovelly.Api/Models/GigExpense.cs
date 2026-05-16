using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GigExpense
{
    public Guid Id { get; set; }
    public Guid GigId { get; set; }
    public Guid? ReimbursementUpdatedByUserId { get; set; }
    public Guid? ReimbursementInvoiceId { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public GigExpenseReimbursementStatus ReimbursementStatus { get; set; } = GigExpenseReimbursementStatus.Unreimbursed;
    public DateTimeOffset? ReimbursedAt { get; set; }
    public DateTimeOffset? ReimbursementUpdatedAt { get; set; }
    public string? ReimbursementMethod { get; set; }
    public string? ReimbursementNote { get; set; }
    public ICollection<ExpenseAttachment> Attachments { get; set; } = new List<ExpenseAttachment>();

    [JsonIgnore]
    public Gig? Gig { get; set; }
    [JsonIgnore]
    public User? ReimbursementUpdatedByUser { get; set; }
    [JsonIgnore]
    public Invoice? ReimbursementInvoice { get; set; }
}
