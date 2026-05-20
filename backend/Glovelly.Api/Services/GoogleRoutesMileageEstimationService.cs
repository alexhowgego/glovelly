using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed class GoogleRoutesMileageEstimationService(
    HttpClient httpClient,
    IOptions<GoogleRoutesMileageSettings> options) : IMileageEstimationService
{
    private const string ProviderName = "google-routes";
    private const string FieldMask = "originIndex,destinationIndex,duration,distanceMeters,status,condition";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GoogleRoutesMileageSettings _settings = options.Value;

    public async Task<MileageEstimateResult> EstimateAsync(
        MileageEstimateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured)
        {
            return MileageEstimateResult.Failure(
                "google_routes_not_configured",
                "Google Routes mileage estimation is not configured.",
                ProviderName);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint)
        {
            Content = JsonContent.Create(BuildRequestBody(request), options: JsonOptions),
        };
        httpRequest.Headers.Add("X-Goog-Api-Key", _settings.ApiKey!.Trim());
        httpRequest.Headers.Add("X-Goog-FieldMask", FieldMask);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return MileageEstimateResult.Failure(
                $"google_routes_http_{(int)response.StatusCode}",
                $"Google Routes returned HTTP {(int)response.StatusCode}.",
                ProviderName);
        }

        GoogleRouteMatrixElement[]? elements;
        try
        {
            elements = JsonSerializer.Deserialize<GoogleRouteMatrixElement[]>(
                responseBody,
                JsonOptions);
        }
        catch (JsonException)
        {
            return MileageEstimateResult.Failure(
                "google_routes_invalid_response",
                "Google Routes returned an invalid response.",
                ProviderName);
        }

        var element = elements?.FirstOrDefault(value => value.OriginIndex == 0 && value.DestinationIndex == 0)
            ?? elements?.FirstOrDefault();
        if (element is null)
        {
            return MileageEstimateResult.Failure(
                "google_routes_empty_response",
                "Google Routes did not return a route estimate.",
                ProviderName);
        }

        if (!string.Equals(element.Condition, "ROUTE_EXISTS", StringComparison.OrdinalIgnoreCase))
        {
            return MileageEstimateResult.Failure(
                NormalizeElementFailureCode(element),
                NormalizeElementFailureMessage(element),
                ProviderName);
        }

        if (!element.DistanceMeters.HasValue)
        {
            return MileageEstimateResult.Failure(
                "google_routes_missing_distance",
                "Google Routes did not return a route distance.",
                ProviderName);
        }

        var multiplier = request.RoundTrip ? 2 : 1;
        var durationSeconds = ParseDurationSeconds(element.Duration);
        return MileageEstimateResult.Success(
            element.DistanceMeters.Value * multiplier,
            durationSeconds.HasValue ? durationSeconds.Value * multiplier : null,
            request.Origin,
            request.Destination,
            ProviderName,
            DateTimeOffset.UtcNow);
    }

    private object BuildRequestBody(MileageEstimateRequest request) => new
    {
        origins = new[]
        {
            new
            {
                waypoint = new
                {
                    address = request.Origin,
                },
            },
        },
        destinations = new[]
        {
            new
            {
                waypoint = BuildDestinationWaypoint(request),
            },
        },
        travelMode = NormalizeEnumSetting(_settings.TravelMode, "DRIVE"),
        routingPreference = NormalizeEnumSetting(_settings.RoutingPreference, "TRAFFIC_UNAWARE"),
    };

    private static object BuildDestinationWaypoint(MileageEstimateRequest request)
    {
        return string.IsNullOrWhiteSpace(request.DestinationPlaceId)
            ? new
            {
                address = request.Destination,
            }
            : new
            {
                placeId = request.DestinationPlaceId,
            };
    }

    private static string NormalizeEnumSetting(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToUpperInvariant();
    }

    private static int? ParseDurationSeconds(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration) || !duration.EndsWith('s'))
        {
            return null;
        }

        var secondsText = duration[..^1];
        if (!decimal.TryParse(secondsText, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        return (int)Math.Round(seconds, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeElementFailureCode(GoogleRouteMatrixElement element)
    {
        if (string.Equals(element.Condition, "ROUTE_NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            return "google_routes_route_not_found";
        }

        if (element.Status?.Code is not null)
        {
            return $"google_routes_status_{element.Status.Code.Value}";
        }

        return "google_routes_route_unavailable";
    }

    private static string NormalizeElementFailureMessage(GoogleRouteMatrixElement element)
    {
        if (!string.IsNullOrWhiteSpace(element.Status?.Message))
        {
            return element.Status.Message;
        }

        return string.Equals(element.Condition, "ROUTE_NOT_FOUND", StringComparison.OrdinalIgnoreCase)
            ? "Google Routes could not find a route for the supplied origin and destination."
            : "Google Routes could not calculate a route.";
    }

    private sealed class GoogleRouteMatrixElement
    {
        [JsonPropertyName("originIndex")]
        public int OriginIndex { get; set; }

        [JsonPropertyName("destinationIndex")]
        public int DestinationIndex { get; set; }

        [JsonPropertyName("condition")]
        public string? Condition { get; set; }

        [JsonPropertyName("distanceMeters")]
        public decimal? DistanceMeters { get; set; }

        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("status")]
        public GoogleRouteStatus? Status { get; set; }
    }

    private sealed class GoogleRouteStatus
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
