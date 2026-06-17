namespace Glovelly.Api.Services;

public sealed class BusinessLifecycleAdvancementTaskState
{
    public DateOnly? NextGigCompletionDate { get; set; }

    public DateOnly? NextInvoiceOverdueDate { get; set; }

    public DateTimeOffset? LastSuccessfulAdvancementUtc { get; set; }
}
