namespace Glovelly.Api.Services;

public sealed record BusinessLifecycleAdvancementResult(
    int CompletedGigs,
    int OverdueInvoices,
    DateOnly? NextGigCompletionDate,
    DateOnly? NextInvoiceOverdueDate);
