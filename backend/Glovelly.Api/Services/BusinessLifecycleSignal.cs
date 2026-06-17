using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed class BusinessLifecycleSignal(
    IScheduledTaskStateStore stateStore,
    ILogger<BusinessLifecycleSignal> logger) : IBusinessLifecycleSignal
{
    public Task TrackGigAsync(Gig gig, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gig);

        return gig.Status == GigStatus.Confirmed
            ? TrackCandidateAsync(GetTransitionDate(gig.Date), isGigCandidate: true, cancellationToken)
            : Task.CompletedTask;
    }

    public Task TrackInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return invoice.Status == InvoiceStatus.Issued
            ? TrackCandidateAsync(GetTransitionDate(invoice.DueDate), isGigCandidate: false, cancellationToken)
            : Task.CompletedTask;
    }

    private async Task TrackCandidateAsync(
        DateOnly candidateDate,
        bool isGigCandidate,
        CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await stateStore.ReadAsync<BusinessLifecycleAdvancementTaskState>(
                ScheduledTaskNames.BusinessLifecycleAdvancement,
                cancellationToken)
                ?? new ScheduledTaskStateEnvelope<BusinessLifecycleAdvancementTaskState>
                {
                    TaskName = ScheduledTaskNames.BusinessLifecycleAdvancement
                };

            var currentDate = isGigCandidate
                ? envelope.State.NextGigCompletionDate
                : envelope.State.NextInvoiceOverdueDate;

            if (currentDate.HasValue && currentDate.Value <= candidateDate)
            {
                return;
            }

            envelope.TaskName = ScheduledTaskNames.BusinessLifecycleAdvancement;
            if (isGigCandidate)
            {
                envelope.State.NextGigCompletionDate = candidateDate;
            }
            else
            {
                envelope.State.NextInvoiceOverdueDate = candidateDate;
            }

            await stateStore.WriteAsync(envelope, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Failed to update scheduled task {TaskName} candidate state. The next safety run can recover from Postgres.",
                ScheduledTaskNames.BusinessLifecycleAdvancement);
        }
    }

    private static DateOnly GetTransitionDate(DateOnly sourceDate)
    {
        return sourceDate == DateOnly.MaxValue ? DateOnly.MaxValue : sourceDate.AddDays(1);
    }
}
