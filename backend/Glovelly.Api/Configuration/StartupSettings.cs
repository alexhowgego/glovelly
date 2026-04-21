namespace Glovelly.Api.Configuration;

internal sealed record StartupSettings(
    string DevCorsPolicy,
    string? GoogleClientId,
    string? GoogleClientSecret,
    string[] AllowedCorsOrigins,
    string? GlovellyConnectionString,
    bool UsePostgres,
    bool IsDevelopment,
    bool IsTesting,
    bool ShouldSeedDevelopmentData)
{
    public static StartupSettings From(IConfiguration configuration, IWebHostEnvironment environment)
    {
        const string devCorsPolicy = "FrontendDevelopment";

        var googleSection = configuration.GetSection("Authentication:Google");
        var googleClientId = googleSection["ClientId"];
        var googleClientSecret = googleSection["ClientSecret"];
        var allowedCorsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var glovellyConnectionString = configuration.GetConnectionString("Glovelly");
        var usePostgres = !string.IsNullOrWhiteSpace(glovellyConnectionString);
        var isDevelopment = environment.IsDevelopment();
        var isTesting = environment.IsEnvironment("Testing");

        return new StartupSettings(
            devCorsPolicy,
            googleClientId,
            googleClientSecret,
            allowedCorsOrigins,
            glovellyConnectionString,
            usePostgres,
            isDevelopment,
            isTesting,
            ShouldSeedDevelopmentData: !usePostgres && !isTesting);
    }
}
