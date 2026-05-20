using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class GigMileageEndpoints
{
    private const decimal MetersPerMile = 1609.344m;

    public static RouteGroupBuilder MapGigMileageEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/mileage-estimate", async (
            Guid id,
            MileageEstimateEndpointRequest request,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext db,
            IMileageEstimationService mileageEstimationService,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == id, cancellationToken);

            if (gig is null)
            {
                return Results.NotFound();
            }

            var sellerProfile = await db.SellerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.UserId == userId.Value, cancellationToken);

            var origin = NormalizeLocation(
                request.OriginPostcode,
                request.OriginCountry,
                sellerProfile?.Address.PostalCode,
                sellerProfile?.Address.Country);
            var destination = NormalizeDestination(request.Destination, gig.Venue);

            var validationErrors = ValidateRequest(origin, destination);
            if (validationErrors is not null)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var estimate = await mileageEstimationService.EstimateAsync(
                new MileageEstimateRequest(
                    origin,
                    destination,
                    request.RoundTrip ?? true,
                    NormalizeOptionalText(request.DestinationPlaceId)),
                cancellationToken);

            if (!estimate.IsSuccess)
            {
                return Results.Problem(
                    detail: estimate.FailureMessage,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = estimate.FailureCode,
                    });
            }

            var distanceMiles = Math.Round(estimate.DistanceMeters / MetersPerMile, 1, MidpointRounding.AwayFromZero);

            return Results.Ok(new
            {
                distanceMiles,
                distanceMeters = estimate.DistanceMeters,
                durationSeconds = estimate.DurationSeconds,
                roundTrip = request.RoundTrip ?? true,
                originLabel = estimate.OriginLabel,
                destinationLabel = estimate.DestinationLabel,
                estimate.Provider,
                calculatedAtUtc = estimate.CalculatedAtUtc,
            });
        });

        return group;
    }

    private static Dictionary<string, string[]>? ValidateRequest(string origin, string destination)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(origin))
        {
            errors["originPostcode"] = ["Set a postcode on the seller profile or include a travel origin postcode in the request."];
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            errors["destination"] = ["Gig venue or destination is required."];
        }

        return errors.Count == 0 ? null : errors;
    }

    private static string NormalizeDestination(string? destination, string fallbackDestination)
    {
        var value = string.IsNullOrWhiteSpace(destination)
            ? fallbackDestination
            : destination;

        return value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeLocation(
        string? requestPostcode,
        string? requestCountry,
        string? fallbackPostcode,
        string? fallbackCountry)
    {
        var postcode = string.IsNullOrWhiteSpace(requestPostcode)
            ? fallbackPostcode
            : requestPostcode;
        if (string.IsNullOrWhiteSpace(postcode))
        {
            return string.Empty;
        }

        var country = string.IsNullOrWhiteSpace(requestCountry)
            ? fallbackCountry
            : requestCountry;

        return string.IsNullOrWhiteSpace(country)
            ? postcode.Trim()
            : $"{postcode.Trim()}, {country.Trim()}";
    }

    internal sealed record MileageEstimateEndpointRequest(
        string? OriginPostcode,
        string? OriginCountry,
        string? Destination,
        string? DestinationPlaceId,
        bool? RoundTrip);
}
