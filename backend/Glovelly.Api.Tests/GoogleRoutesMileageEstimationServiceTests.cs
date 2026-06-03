using System.Net;
using System.Text;
using System.Text.Json;
using Glovelly.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GoogleRoutesMileageEstimationServiceTests
{
    [Fact]
    public async Task EstimateAsync_SendsRouteMatrixRequestAndReturnsRoundTripEstimate()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return JsonResponse("""
                [
                  {
                    "originIndex": 0,
                    "destinationIndex": 0,
                    "condition": "ROUTE_EXISTS",
                    "distanceMeters": 10000,
                    "duration": "123s"
                  }
                ]
                """);
        });

        var result = await service.EstimateAsync(new MileageEstimateRequest(
            "BS1 1AA, GB",
            "Town Hall, Bristol",
            RoundTrip: true), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(20000m, result.DistanceMeters);
        Assert.Equal(246, result.DurationSeconds);
        Assert.Equal("BS1 1AA, GB", result.OriginLabel);
        Assert.Equal("Town Hall, Bristol", result.DestinationLabel);
        Assert.Equal("google-routes", result.Provider);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://routes.test/computeRouteMatrix", capturedRequest.RequestUri!.ToString());
        Assert.True(capturedRequest.Headers.TryGetValues("X-Goog-Api-Key", out var apiKeyValues));
        Assert.Equal("test-api-key", Assert.Single(apiKeyValues));
        Assert.True(capturedRequest.Headers.TryGetValues("X-Goog-FieldMask", out var fieldMaskValues));
        Assert.Equal(
            "originIndex,destinationIndex,duration,distanceMeters,status,condition",
            Assert.Single(fieldMaskValues));

        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        Assert.Equal("DRIVE", root.GetProperty("travelMode").GetString());
        Assert.Equal("TRAFFIC_UNAWARE", root.GetProperty("routingPreference").GetString());
        Assert.Equal(
            "BS1 1AA, GB",
            root.GetProperty("origins")[0].GetProperty("waypoint").GetProperty("address").GetString());
        Assert.Equal(
            "Town Hall, Bristol",
            root.GetProperty("destinations")[0].GetProperty("waypoint").GetProperty("address").GetString());
    }

    [Fact]
    public async Task EstimateAsync_WhenApiKeyMissing_DoesNotCallGoogle()
    {
        var wasCalled = false;
        var service = CreateService(
            (_, _) =>
            {
                wasCalled = true;
                return Task.FromResult(JsonResponse("[]"));
            },
            apiKey: null);

        var result = await service.EstimateAsync(new MileageEstimateRequest(
            "BS1 1AA",
            "Town Hall",
            RoundTrip: false), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("google_routes_not_configured", result.FailureCode);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task EstimateAsync_WithDestinationPlaceId_SendsPlaceIdWaypoint()
    {
        string? capturedBody = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("""
                [
                  {
                    "originIndex": 0,
                    "destinationIndex": 0,
                    "condition": "ROUTE_EXISTS",
                    "distanceMeters": 10000,
                    "duration": "123s"
                  }
                ]
                """);
        });

        var result = await service.EstimateAsync(new MileageEstimateRequest(
            "BS1 1AA, GB",
            "Town Hall, Bristol",
            RoundTrip: false,
            DestinationPlaceId: "ChIJN1t_tDeuEmsRUsoyG83frY4"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);

        using var document = JsonDocument.Parse(capturedBody!);
        var destinationWaypoint = document.RootElement
            .GetProperty("destinations")[0]
            .GetProperty("waypoint");
        Assert.Equal("ChIJN1t_tDeuEmsRUsoyG83frY4", destinationWaypoint.GetProperty("placeId").GetString());
        Assert.False(destinationWaypoint.TryGetProperty("address", out _));
    }

    [Fact]
    public async Task EstimateAsync_WhenRouteNotFound_ReturnsProviderFailure()
    {
        var service = CreateService((_, _) => Task.FromResult(JsonResponse("""
            [
              {
                "originIndex": 0,
                "destinationIndex": 0,
                "condition": "ROUTE_NOT_FOUND",
                "status": {
                  "code": 5,
                  "message": "No route found."
                }
              }
            ]
            """)));

        var result = await service.EstimateAsync(new MileageEstimateRequest(
            "BS1 1AA",
            "Somewhere",
            RoundTrip: false), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("google_routes_route_not_found", result.FailureCode);
        Assert.Equal("No route found.", result.FailureMessage);
    }

    [Fact]
    public async Task EstimateAsync_WhenGoogleReturnsHttpError_ReturnsProviderFailure()
    {
        var service = CreateService((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("forbidden", Encoding.UTF8, "text/plain"),
        }));

        var result = await service.EstimateAsync(new MileageEstimateRequest(
            "BS1 1AA",
            "Town Hall",
            RoundTrip: false), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("google_routes_http_403", result.FailureCode);
    }

    private static GoogleRoutesMileageEstimationService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        string? apiKey = "test-api-key")
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler));
        var settings = Options.Create(new GoogleRoutesMileageSettings
        {
            ApiKey = apiKey,
            Endpoint = "https://routes.test/computeRouteMatrix",
        });

        return new GoogleRoutesMileageEstimationService(httpClient, settings);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
