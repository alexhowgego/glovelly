namespace Glovelly.Api.Services;

public interface IMileageEstimationService
{
    Task<MileageEstimateResult> EstimateAsync(
        MileageEstimateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record MileageEstimateRequest(
    string Origin,
    string Destination,
    bool RoundTrip,
    string? DestinationPlaceId = null);

public sealed record MileageEstimateResult(
    bool IsSuccess,
    decimal DistanceMeters,
    int? DurationSeconds,
    string OriginLabel,
    string DestinationLabel,
    string Provider,
    DateTimeOffset CalculatedAtUtc,
    string? FailureCode = null,
    string? FailureMessage = null)
{
    public static MileageEstimateResult Success(
        decimal distanceMeters,
        int? durationSeconds,
        string originLabel,
        string destinationLabel,
        string provider,
        DateTimeOffset calculatedAtUtc) =>
        new(
            true,
            distanceMeters,
            durationSeconds,
            originLabel,
            destinationLabel,
            provider,
            calculatedAtUtc);

    public static MileageEstimateResult Failure(
        string failureCode,
        string failureMessage,
        string provider = "none") =>
        new(
            false,
            0m,
            null,
            string.Empty,
            string.Empty,
            provider,
            DateTimeOffset.UtcNow,
            failureCode,
            failureMessage);
}
