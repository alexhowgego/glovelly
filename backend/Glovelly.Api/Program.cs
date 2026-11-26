using Glovelly.Api.Configuration;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Hubs;
using Glovelly.Api.Services;

var developmentBootstrap = args.Length >= 3 &&
    args[0] == "grant-super-admin" &&
    args[1] == "--development-in-memory" &&
    args[2] == "--serve";

if (args.Length > 0 && args[0] == "grant-super-admin" && !developmentBootstrap)
{
    Environment.ExitCode = await AdministrativeBootstrapCommand.RunAsync(args);
    return;
}

var builder = WebApplication.CreateBuilder(developmentBootstrap ? args[3..] : args);
var startupSettings = StartupSettings.From(builder.Configuration, builder.Environment);

if (developmentBootstrap)
{
    DevelopmentAdministrativeBootstrap.ValidateEnvironment(startupSettings);
}

builder.Services.AddGlovellyInfrastructure(builder.Configuration, startupSettings);
builder.Services.AddGlovellyAuthentication(startupSettings);

var app = builder.Build();

await app.InitializeDatabaseAsync(
    builder.Configuration,
    startupSettings);

if (developmentBootstrap)
{
    await using var scope = app.Services.CreateAsyncScope();
    await DevelopmentAdministrativeBootstrap.GrantAsync(
        startupSettings,
        builder.Configuration,
        scope.ServiceProvider.GetRequiredService<Glovelly.Api.Data.AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<AdministrativePermissionAssignmentService>());
    app.Logger.LogInformation("Explicit local in-memory Super Administrator bootstrap completed.");
}

app.UseGlovellyHttpPipeline(startupSettings);
app.MapAppMetadataEndpoints(startupSettings);
app.MapAuthEndpoints(startupSettings);
app.MapTestAuthEndpoints(startupSettings);
app.MapAccessEndpoints(startupSettings);
app.MapGoogleDriveIntegrationEndpoints();
app.MapGoogleIntegrationOAuthEndpoints(startupSettings);
app.MapGoogleCalendarIntegrationEndpoints();
app.MapMcpOAuthEndpoints();
app.MapMcpEndpoints();
app.MapInvoiceEmailTemplateEndpoints();
app.MapForScoreLibraryEndpoints();
app.MapCrudEndpoints();
app.MapEntitlementEndpoints();
app.MapExpenseStatementEndpoints();
app.MapAdminEndpoints(startupSettings);
app.MapAdministrativePermissionEndpoints();
app.MapLicenceEndpoints();
app.MapAccessRequestAdminEndpoints(startupSettings);
app.MapHub<WorkspaceEventsHub>("/workspace-events");
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
