using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Configuration;

public static class WorkerServiceCollectionExtensions
{
    public static IServiceCollection AddGlovellyWorkerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var settings = StartupSettings.From(configuration, environment);

        services.AddSingleton(settings);
        services.AddOptions<EmailSettings>()
            .Bind(configuration.GetSection("Email"));
        services.AddGlovellyApplicationServices();
        services.AddScoped<IGoogleTokenProtector, GoogleTokenProtector>();
        services.AddHttpClient<IGoogleOAuthTokenClient, GoogleOAuthTokenClient>();
        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName("Glovelly");
        if (settings.UsePostgres)
        {
            dataProtectionBuilder.PersistKeysToDbContext<AppDbContext>();
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            if (settings.UsePostgres)
            {
                options.UseNpgsql(settings.GlovellyConnectionString);
                return;
            }

            options.UseInMemoryDatabase("Glovelly");
        });
        return services;
    }
}
