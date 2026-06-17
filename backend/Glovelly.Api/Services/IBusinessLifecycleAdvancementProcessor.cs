namespace Glovelly.Api.Services;

public interface IBusinessLifecycleAdvancementProcessor
{
    Task<BusinessLifecycleAdvancementResult> AdvanceAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
