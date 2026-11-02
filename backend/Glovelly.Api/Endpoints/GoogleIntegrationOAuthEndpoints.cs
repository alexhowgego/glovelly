using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Glovelly.Api.Endpoints;

internal static class GoogleIntegrationOAuthEndpoints
{
    public static IEndpointRouteBuilder MapGoogleIntegrationOAuthEndpoints(this IEndpointRouteBuilder app, StartupSettings settings)
    {
        MapIntegration(app, "/integrations/google-sheets", GoogleIntegrationOAuth.Sheets, settings, true);
        MapIntegration(app, "/integrations/google-docs", GoogleIntegrationOAuth.Docs, settings, true);
        MapIntegration(app, "/integrations/google-drive", GoogleIntegrationOAuth.Drive, settings, false);
        MapIntegration(app, "/integrations/google-calendar", GoogleIntegrationOAuth.Calendar, settings, false);

        app.MapGet("/integrations/google/callback", async (
            string? code, string? state, string? error, string? error_description,
            ClaimsPrincipal principal, ICurrentUserAccessor currentUserAccessor, AppDbContext dbContext,
            IDataProtectionProvider protection, IGoogleOAuthTokenClient tokenClient,
            IGoogleConnectionService connections, IGoogleCalendarIntegrationService calendar, ICalendarSyncWorkQueue workQueue,
            ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
            await GoogleIntegrationOAuth.CallbackAsync(settings, code, state, error, error_description, principal,
                currentUserAccessor, dbContext, protection, tokenClient, connections, calendar, workQueue, loggerFactory, cancellationToken))
            .WithTags("Integrations").RequireAuthorization(GlovellyPolicies.GlovellyUser);
        return app;
    }

    private static void MapIntegration(IEndpointRouteBuilder app, string path, GoogleIntegrationOAuth.Integration integration, StartupSettings settings, bool sharedDisconnect)
    {
        var group = app.MapGroup(path).WithTags("Integrations").RequireAuthorization(GlovellyPolicies.GlovellyUser);
        group.MapGet("/connect", async (ClaimsPrincipal principal, ICurrentUserAccessor users, AppDbContext db,
            IDataProtectionProvider protection, CancellationToken token) =>
            await GoogleIntegrationOAuth.ConnectAsync(settings, integration, principal, users, db, protection, token));
        if (sharedDisconnect)
            group.MapPost("/disconnect", async (ClaimsPrincipal principal, ICurrentUserAccessor users, AppDbContext db, CancellationToken token) =>
                await GoogleIntegrationOAuth.DisconnectAsync(integration, principal, users, db, token));
    }
}
