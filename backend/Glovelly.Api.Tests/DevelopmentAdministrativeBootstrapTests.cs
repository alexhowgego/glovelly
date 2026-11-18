using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class DevelopmentAdministrativeBootstrapTests
{
    [Fact]
    public async Task ExplicitBootstrapGrantsSeededAdministratorAndRecordsOperatorAudit()
    {
        var token = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        var configuration = Configuration("local-subject");
        await DevelopmentDataSeeder.SeedAsync(db, configuration);
        var user = await db.Users.SingleAsync(value => value.GoogleSubject == "local-subject", token);
        Assert.Equal([AdministrativePermissionSet.UserAdministrator],
            await db.UserAdministrativePermissionSets.Where(value => value.UserId == user.Id)
                .Select(value => value.PermissionSet).ToArrayAsync(token));
        Assert.Empty(await db.AdministrativePermissionChanges.ToListAsync(token));

        var service = new AdministrativePermissionAssignmentService(db, new AdministrativeMutationCoordinator(db), TimeProvider.System);
        await DevelopmentAdministrativeBootstrap.GrantAsync(Settings(), configuration, db, service, token);
        await DevelopmentAdministrativeBootstrap.GrantAsync(Settings(), configuration, db, service, token);

        var sets = await db.UserAdministrativePermissionSets.Where(value => value.UserId == user.Id)
            .Select(value => value.PermissionSet).ToArrayAsync(token);
        Assert.Contains(AdministrativePermissionSet.UserAdministrator, sets);
        Assert.Contains(AdministrativePermissionSet.SuperAdministrator, sets);
        var audit = Assert.Single(await db.AdministrativePermissionChanges.ToListAsync(token));
        Assert.Equal(user.Id, audit.TargetUserId);
        Assert.Equal("Operator", audit.Source);
        Assert.Equal("local-development-operator", audit.ActorIdentity);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, false, true)]
    public void BootstrapRejectsNonDevelopmentStagingTestingAndPostgres(
        bool development, bool staging, bool testing, bool postgres)
    {
        var settings = Settings() with
        {
            IsDevelopment = development,
            IsStaging = staging,
            IsTesting = testing,
            UsePostgres = postgres,
        };
        Assert.Throws<InvalidOperationException>(() => DevelopmentAdministrativeBootstrap.ValidateEnvironment(settings));
    }

    [Fact]
    public async Task BootstrapRequiresConfiguredAndSeededSubject()
    {
        var token = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        var service = new AdministrativePermissionAssignmentService(db, new AdministrativeMutationCoordinator(db), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentAdministrativeBootstrap.GrantAsync(Settings(), Configuration(null), db, service, token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentAdministrativeBootstrap.GrantAsync(Settings(), Configuration("not-seeded"), db, service, token));
        Assert.Empty(await db.AdministrativePermissionChanges.ToListAsync(token));
    }

    [Fact]
    public async Task BootstrapRejectsNonInMemoryProviderEvenIfSettingsClaimInMemory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;
        await using var db = new AppDbContext(options);
        var service = new AdministrativePermissionAssignmentService(db, new AdministrativeMutationCoordinator(db), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentAdministrativeBootstrap.GrantAsync(
                Settings(), Configuration("subject"), db, service, TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IConfiguration Configuration(string? subject) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentSeeding:AdminGoogleSubject"] = subject,
        }).Build();

    private static StartupSettings Settings() => new(
        DevCorsPolicy: "test", GoogleClientId: null, GoogleClientSecret: null,
        AllowedCorsOrigins: [], GlovellyConnectionString: null, DeploymentName: null,
        BuildCommitId: null, BuildTimestamp: null, PublicBaseUri: new Uri("http://localhost"),
        UsePostgres: false, IsDevelopment: true, IsStaging: false, IsTesting: false,
        ShouldSeedDevelopmentData: true, ShouldSeedUatData: false);
}
