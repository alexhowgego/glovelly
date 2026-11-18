using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public static class DevelopmentAdministrativeBootstrap
{
    public static void ValidateEnvironment(StartupSettings settings)
    {
        if (!settings.IsDevelopment || settings.IsStaging || settings.IsTesting ||
            settings.UsePostgres || !settings.ShouldSeedDevelopmentData)
        {
            throw new InvalidOperationException(
                "The local Super Administrator bootstrap requires Development and an in-memory database.");
        }
    }

    public static async Task GrantAsync(
        StartupSettings settings,
        IConfiguration configuration,
        AppDbContext db,
        AdministrativePermissionAssignmentService assignments,
        CancellationToken cancellationToken = default)
    {
        ValidateEnvironment(settings);
        if (!db.Database.IsInMemory())
        {
            throw new InvalidOperationException("The local Super Administrator bootstrap requires an in-memory database.");
        }

        var subject = configuration["DevelopmentSeeding:AdminGoogleSubject"]?.Trim();
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException(
                "Configure DevelopmentSeeding:AdminGoogleSubject before requesting local Super Administrator access.");
        }

        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(value => value.GoogleSubject == subject, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("The configured development administrator was not seeded.");
        }

        var result = await assignments.BootstrapSuperAsync(
            user.Id,
            new AdministrativeChangeActor(null, "local-development-operator", "Operator"),
            "Explicit local in-memory development bootstrap",
            cancellationToken);
        if (result is not (AdministrativeAssignmentResult.Changed or AdministrativeAssignmentResult.Unchanged))
        {
            throw new InvalidOperationException($"The local Super Administrator bootstrap failed: {result}.");
        }
    }
}
