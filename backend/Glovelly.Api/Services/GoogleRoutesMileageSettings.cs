namespace Glovelly.Api.Services;

public sealed class GoogleRoutesMileageSettings
{
    public const string SectionName = "Mileage:GoogleRoutes";

    public string? ApiKey { get; set; }
    public string Endpoint { get; set; } =
        "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix";
    public string TravelMode { get; set; } = "DRIVE";
    public string RoutingPreference { get; set; } = "TRAFFIC_UNAWARE";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
