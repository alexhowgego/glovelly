using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Services;

namespace Glovelly.Api.Endpoints;

internal static class EntitlementEndpoints
{
    public static IEndpointRouteBuilder MapEntitlementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/entitlements/me", async (
            ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor,
            EntitlementService service, HttpResponse response, CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }
            response.Headers.CacheControl = "no-store";
            return Results.Ok(await service.GetAsync(userId.Value, cancellationToken));
        }).RequireAuthorization(GlovellyPolicies.GlovellyUser);
        return app;
    }
}

internal static class EntitlementProblems
{
    public static IResult AllowanceExhausted(EntitlementSnapshot snapshot) =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden,
            title: "Monthly invoice allowance reached",
            detail: "You can keep editing drafts. Wait until the allowance resets or contact a licence administrator to change your plan.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "invoice_allowance_exhausted",
                ["effectivePlan"] = snapshot.EffectivePlan.Code,
                ["usage"] = snapshot.InvoiceIssuance.Used,
                ["limit"] = snapshot.InvoiceIssuance.Limit,
                ["remaining"] = snapshot.InvoiceIssuance.Remaining,
                ["resetsAtUtc"] = snapshot.InvoiceIssuance.ResetsAtUtc,
            });

    public static IResult FeatureNotEntitled() =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden,
            title: "Reporting requires Solo or Pro",
            detail: "Your records remain available. Contact a licence administrator to change your plan.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "feature_not_entitled",
                ["feature"] = "PaidIncomeSummary",
            });
}
