namespace Glovelly.Api.Configuration;

public sealed record StartupSettings(
    string DevCorsPolicy,
    string? GoogleClientId,
    string? GoogleClientSecret,
    string[] AllowedCorsOrigins,
    string? GlovellyConnectionString,
    string? DeploymentName,
    string? BuildCommitId,
    string? BuildTimestamp,
    bool UsePostgres,
    bool IsDevelopment,
    bool IsStaging,
    bool IsTesting,
    bool ShouldSeedDevelopmentData,
    bool ShouldSeedUatData)
{
    public static StartupSettings From(IConfiguration configuration, IHostEnvironment environment)
    {
        const string devCorsPolicy = "FrontendDevelopment";

        var googleSection = configuration.GetSection("Authentication:Google");
        var googleClientId = googleSection["ClientId"];
        var googleClientSecret = googleSection["ClientSecret"];
        var allowedCorsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var glovellyConnectionString = configuration.GetConnectionString("Glovelly");
        var deploymentName = configuration["App:DeploymentName"];
        var buildCommitId = configuration["App:BuildCommitId"];
        var buildTimestamp = configuration["App:BuildTimestamp"];
        var usePostgres = !string.IsNullOrWhiteSpace(glovellyConnectionString);
        var isDevelopment = environment.IsDevelopment();
        var isStaging = environment.IsStaging() ||
                        string.Equals(deploymentName?.Trim(), "Staging", StringComparison.OrdinalIgnoreCase);
        var isTesting = environment.IsEnvironment("Testing");

        return new StartupSettings(
            devCorsPolicy,
            googleClientId,
            googleClientSecret,
            allowedCorsOrigins,
            glovellyConnectionString,
            deploymentName,
            buildCommitId,
            buildTimestamp,
            usePostgres,
            isDevelopment,
            isStaging,
            isTesting,
            ShouldSeedDevelopmentData: !usePostgres && !isTesting,
            ShouldSeedUatData: isStaging);
    }
}
