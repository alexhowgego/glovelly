using Glovelly.Api.Configuration;
using Glovelly.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
var startupSettings = StartupSettings.From(builder.Configuration, builder.Environment);

builder.Services.AddGlovellyInfrastructure(builder.Configuration, startupSettings);
builder.Services.AddGlovellyAuthentication(startupSettings);

var app = builder.Build();

await app.InitializeDatabaseAsync(builder.Configuration, startupSettings.ShouldSeedDevelopmentData);

app.UseGlovellyHttpPipeline(startupSettings);
app.MapAppMetadataEndpoints(startupSettings);
app.MapAuthEndpoints(startupSettings);
app.MapAccessEndpoints(startupSettings);
app.MapGoogleDriveIntegrationEndpoints();
app.MapCrudEndpoints();
app.MapAdminEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
