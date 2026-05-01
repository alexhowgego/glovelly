using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace Glovelly.Api.Configuration;

internal static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGlovellyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        StartupSettings settings)
    {
        var accessRequestSettings = configuration.GetSection(AccessRequestProtectionSettings.SectionName)
            .Get<AccessRequestProtectionSettings>() ?? new AccessRequestProtectionSettings();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddOptions<EmailSettings>()
            .Bind(configuration.GetSection("Email"));
        services.AddOptions<AccessRequestProtectionSettings>()
            .Bind(configuration.GetSection(AccessRequestProtectionSettings.SectionName));
        services.AddGlovellyApplicationServices();
        services.AddHttpClient<IGoogleDriveOAuthTokenExchanger, GoogleDriveOAuthTokenExchanger>();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<IClaimsTransformation, GoogleOidcClaimsTransformation>();
        services.AddDbContext<AppDbContext>(options =>
        {
            if (settings.UsePostgres)
            {
                options.UseNpgsql(settings.GlovellyConnectionString);
                return;
            }

            options.UseInMemoryDatabase("Glovelly");
        });
        services.AddCors(options =>
        {
            options.AddPolicy(settings.DevCorsPolicy, policy =>
            {
                if (settings.AllowedCorsOrigins.Length > 0)
                {
                    policy.WithOrigins(settings.AllowedCorsOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(GlovellyPolicies.GlovellyUser, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(GlovellyClaimTypes.UserId);
            });
            options.AddPolicy(GlovellyPolicies.AdminUser, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(GlovellyClaimTypes.UserId);
                policy.RequireRole(UserRole.Admin.ToString());
            });
        });
        services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Glovelly.AccessRequests");
                logger.LogWarning(
                    "Access request rate limit triggered for IP {RemoteIpAddress}.",
                    context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "(unknown)");
                context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    "{\"message\":\"Access request submitted.\"}",
                    cancellationToken);
            };
            options.AddPolicy<string>("PublicAccessRequest", httpContext =>
            {
                var remoteIp = AccessRequestRequestContext.ResolveRateLimitPartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    remoteIp,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = accessRequestSettings.PerIpShortWindowPermitLimit,
                        Window = accessRequestSettings.PerIpShortWindow,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }
}
