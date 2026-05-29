using Glovelly.Api.Configuration;
using Glovelly.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
var startupSettings = StartupSettings.From(builder.Configuration, builder.Environment);

builder.Services.AddGlovellyInfrastructure(builder.Configuration, startupSettings);
builder.Services.AddGlovellyAuthentication(startupSettings);

var app = builder.Build();

await app.InitializeDatabaseAsync(
    builder.Configuration,
    startupSettings.ShouldSeedDevelopmentData,
    startupSettings.ShouldSeedUatData);

app.UseGlovellyHttpPipeline(startupSettings);
app.MapAppMetadataEndpoints(startupSettings);
app.MapAuthEndpoints(startupSettings);
app.MapTestAuthEndpoints(startupSettings);
app.MapAccessEndpoints(startupSettings);
app.MapGoogleDriveIntegrationEndpoints(startupSettings);
app.MapGoogleCalendarIntegrationEndpoints(startupSettings);
app.MapMcpOAuthEndpoints();
app.MapMcpEndpoints();
app.MapCrudEndpoints();
app.MapExpenseStatementEndpoints();
app.MapAdminEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
