namespace Glovelly.Api.Services;

public sealed class DisabledMileageEstimationService : IMileageEstimationService
{
    public Task<MileageEstimateResult> EstimateAsync(
        MileageEstimateRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MileageEstimateResult.Failure(
            "mileage_estimation_not_configured",
            "Automatic mileage estimation is not configured."));
    }
}
