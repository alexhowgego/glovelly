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
    Uri PublicBaseUri,
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
        var publicBaseUri = GetPublicBaseUri(configuration["App:PublicBaseUrl"], environment, allowedCorsOrigins);
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
            publicBaseUri,
            usePostgres,
            isDevelopment,
            isStaging,
            isTesting,
            ShouldSeedDevelopmentData: !usePostgres && !isTesting,
            ShouldSeedUatData: isStaging);
    }

    public string BuildPublicUrl(string path)
    {
        return new Uri(PublicBaseUri, path).ToString();
    }

    public bool IsAllowedReturnUri(Uri uri)
    {
        if (Uri.Compare(uri, PublicBaseUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
        {
            return true;
        }

        return (IsDevelopment || IsTesting) && uri.IsLoopback;
    }

    private static Uri GetPublicBaseUri(string? configuredValue, IHostEnvironment environment, string[] allowedCorsOrigins)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            if (environment.IsDevelopment())
            {
                configuredValue = allowedCorsOrigins.FirstOrDefault() ?? "http://localhost:5173";
            }
            else if (environment.IsEnvironment("Testing"))
            {
                configuredValue = "http://localhost";
            }
            else
            {
                throw new InvalidOperationException("App:PublicBaseUrl must be configured outside Development and Testing.");
            }
        }

        if (!Uri.TryCreate(configuredValue.Trim(), UriKind.Absolute, out var publicBaseUri) ||
            !string.IsNullOrEmpty(publicBaseUri.UserInfo) ||
            !string.IsNullOrEmpty(publicBaseUri.Query) ||
            !string.IsNullOrEmpty(publicBaseUri.Fragment) ||
            publicBaseUri.AbsolutePath != "/" ||
            (!environment.IsDevelopment() && !environment.IsEnvironment("Testing") && publicBaseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("App:PublicBaseUrl must be an absolute origin and use HTTPS outside Development and Testing.");
        }

        return new Uri(publicBaseUri.GetLeftPart(UriPartial.Authority));
    }

}
