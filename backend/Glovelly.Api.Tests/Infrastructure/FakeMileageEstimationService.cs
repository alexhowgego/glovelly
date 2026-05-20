using Glovelly.Api.Services;

namespace Glovelly.Api.Tests.Infrastructure;

internal sealed class FakeMileageEstimationService : IMileageEstimationService
{
    public MileageEstimateResult Result { get; set; } = MileageEstimateResult.Failure(
        "test_mileage_estimation_unavailable",
        "Test mileage estimation result was not configured.",
        "test");

    public MileageEstimateRequest? LastRequest { get; private set; }

    public Task<MileageEstimateResult> EstimateAsync(
        MileageEstimateRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(Result);
    }

    public void Reset()
    {
        Result = MileageEstimateResult.Failure(
            "test_mileage_estimation_unavailable",
            "Test mileage estimation result was not configured.",
            "test");
        LastRequest = null;
    }
}
