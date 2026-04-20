using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace Glovelly.Api.Configuration;

internal static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGlovellyInfrastructure(this IServiceCollection services, StartupSettings settings)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddGlovellyApplicationServices();
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

        return services;
    }
}
