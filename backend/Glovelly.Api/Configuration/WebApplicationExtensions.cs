using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;

namespace Glovelly.Api.Configuration;

internal static class WebApplicationExtensions
{
    public static async Task InitializeDatabaseAsync(
        this WebApplication app,
        IConfiguration configuration,
        bool shouldSeedDevelopmentData)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (shouldSeedDevelopmentData)
        {
            await AppDbSeeder.SeedAsync(dbContext, configuration);
            return;
        }

        await dbContext.Database.EnsureCreatedAsync();
    }

    public static WebApplication UseGlovellyHttpPipeline(this WebApplication app, StartupSettings settings)
    {
        if (settings.IsDevelopment)
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();
            app.UseCors(settings.DevCorsPolicy);
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.Use(async (context, next) =>
        {
            if (AuthFlowSupport.IsApiRequest(context.Request))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
                    context.Response.Headers["Pragma"] = "no-cache";
                    context.Response.Headers["Expires"] = "Thu, 01 Jan 1970 00:00:00 GMT";
                    return Task.CompletedTask;
                });
            }

            await next();
        });

        return app;
    }
}
