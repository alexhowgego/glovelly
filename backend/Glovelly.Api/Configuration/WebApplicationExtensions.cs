using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Services;

namespace Glovelly.Api.Configuration;

internal static class WebApplicationExtensions
{
    public static async Task InitializeDatabaseAsync(
        this WebApplication app,
        IConfiguration configuration,
        StartupSettings settings)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (settings.ShouldSeedDevelopmentData)
        {
            var attachmentStore = scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
            var blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
            await DevelopmentDataSeeder.SeedAsync(dbContext, configuration, attachmentStore, blobStore);
        }
        else if (!settings.UsePostgres)
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        if (settings.ShouldSeedUatData)
        {
            await UatRegressionDataSeeder.SeedAsync(dbContext);
        }
    }

    public static WebApplication UseGlovellyHttpPipeline(this WebApplication app, StartupSettings settings)
    {
        if (settings.IsDevelopment)
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseWhen(
                context => !context.Request.Path.StartsWithSegments("/mcp"),
                branch => branch.UseHttpsRedirection());
            app.UseCors(settings.DevCorsPolicy);
        }

        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Glovelly.Api.Services.InvoiceAllowanceExceededException exception) when (!context.Response.HasStarted)
            {
                await EntitlementProblems.AllowanceExhausted(exception.Snapshot).ExecuteAsync(context);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) when (!context.Response.HasStarted)
            {
                await Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "The record changed",
                    detail: "Reload the latest record and try again.",
                    extensions: new Dictionary<string, object?> { ["code"] = "concurrent_update" }).ExecuteAsync(context);
            }
        });
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.Use(async (context, next) =>
        {
            if (AuthFlowSupport.IsApiRequest(context.Request))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers["Cache-Control"] = "private, no-store, no-cache, max-age=0";
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
