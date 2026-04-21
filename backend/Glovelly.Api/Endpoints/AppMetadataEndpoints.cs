using Glovelly.Api.Configuration;

namespace Glovelly.Api.Endpoints;

internal static class AppMetadataEndpoints
{
    public static IEndpointRouteBuilder MapAppMetadataEndpoints(
        this IEndpointRouteBuilder app,
        StartupSettings settings)
    {
        var metadata = app.MapGroup("/app").AllowAnonymous();

        metadata.MapGet("/metadata", () =>
        {
            var deploymentName = string.IsNullOrWhiteSpace(settings.DeploymentName)
                ? null
                : settings.DeploymentName.Trim();
            var title = string.Equals(deploymentName, "Staging", StringComparison.OrdinalIgnoreCase)
                ? "Glovelly - Staging"
                : "Glovelly";

            return Results.Ok(new
            {
                title,
                deploymentName,
                commitId = string.IsNullOrWhiteSpace(settings.BuildCommitId)
                    ? null
                    : settings.BuildCommitId.Trim(),
                buildTimestamp = string.IsNullOrWhiteSpace(settings.BuildTimestamp)
                    ? null
                    : settings.BuildTimestamp.Trim(),
            });
        });

        return app;
    }
}
