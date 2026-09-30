using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class GigQuickCaptureCandidateEndpoints
{
    public static RouteGroupBuilder MapGigQuickCaptureCandidateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/quick-capture-candidates", async (
            string? continuation,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IOptions<QuickCaptureSettings> quickCaptureOptions,
            TimeProvider timeProvider) =>
        {
            try
            {
                var result = await GigQuickCaptureSupport.QueryCandidatesAsync(
                    db,
                    currentUserAccessor.TryGetUserId(user),
                    DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
                    GigQuickCaptureSupport.NormalizeSettings(quickCaptureOptions.Value),
                    continuation);

                return Results.Ok(new
                {
                    candidates = GigQuickCaptureSupport.ToCandidateResponses(result.Candidates, null),
                    result.HasMore,
                    result.Continuation,
                });
            }
            catch (InvalidOperationException)
            {
                return EndpointSupport.ValidationProblem("continuation", "The candidate continuation is invalid.");
            }
        });

        return group;
    }
}
