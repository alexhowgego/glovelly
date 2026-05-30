using Glovelly.Api.Configuration;
using Glovelly.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddGlovellyWorkerInfrastructure(builder.Configuration, builder.Environment);

using var host = builder.Build();
return await RunAsync(args, host.Services);

static async Task<int> RunAsync(string[] args, IServiceProvider services)
{
    if (args is ["calendar-sync", "drain", .. var remainingArgs])
    {
        return await RunCalendarSyncDrainAsync(remainingArgs, services);
    }

    PrintUsage();
    return 2;
}

static async Task<int> RunCalendarSyncDrainAsync(string[] args, IServiceProvider services)
{
    if (!TryParseDrainOptions(args, out var options, out var error))
    {
        Console.Error.WriteLine(error);
        PrintUsage();
        return 2;
    }

    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Glovelly.Worker.CalendarSync");
    var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();

    var result = await drainer.DrainAsync(options);
    logger.LogInformation(
        "Calendar sync drain complete: {Processed} processed, {Succeeded} succeeded, {Retried} retried, {Failed} failed, {Skipped} skipped, {Recovered} recovered.",
        result.Processed,
        result.Succeeded,
        result.Retried,
        result.Failed,
        result.Skipped,
        result.Recovered);

    return 0;
}

static bool TryParseDrainOptions(
    string[] args,
    out CalendarSyncDrainOptions options,
    out string? error)
{
    var maxItems = 100;
    TimeSpan? maxDuration = null;
    string? ownerId = null;
    TimeSpan? processingTimeout = null;

    for (var index = 0; index < args.Length; index++)
    {
        var arg = args[index];
        switch (arg)
        {
            case "--max-items":
                if (!TryReadInt(args, ref index, out maxItems, out error) || maxItems <= 0)
                {
                    error ??= "--max-items must be a positive integer.";
                    options = default!;
                    return false;
                }

                break;
            case "--max-duration-seconds":
                if (!TryReadInt(args, ref index, out var maxDurationSeconds, out error) || maxDurationSeconds <= 0)
                {
                    error ??= "--max-duration-seconds must be a positive integer.";
                    options = default!;
                    return false;
                }

                maxDuration = TimeSpan.FromSeconds(maxDurationSeconds);
                break;
            case "--owner-id":
                if (!TryReadString(args, ref index, out ownerId, out error))
                {
                    options = default!;
                    return false;
                }

                break;
            case "--processing-timeout-seconds":
                if (!TryReadInt(args, ref index, out var processingTimeoutSeconds, out error) || processingTimeoutSeconds <= 0)
                {
                    error ??= "--processing-timeout-seconds must be a positive integer.";
                    options = default!;
                    return false;
                }

                processingTimeout = TimeSpan.FromSeconds(processingTimeoutSeconds);
                break;
            default:
                error = $"Unknown argument: {arg}";
                options = default!;
                return false;
        }
    }

    options = new CalendarSyncDrainOptions(maxItems, maxDuration, ownerId, processingTimeout);
    error = null;
    return true;
}

static bool TryReadInt(string[] args, ref int index, out int value, out string? error)
{
    if (!TryReadString(args, ref index, out var text, out error))
    {
        value = default;
        return false;
    }

    if (int.TryParse(text, out value))
    {
        return true;
    }

    error = $"Expected integer value after {args[index - 1]}.";
    return false;
}

static bool TryReadString(string[] args, ref int index, out string value, out string? error)
{
    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
    {
        value = string.Empty;
        error = $"Expected value after {args[index]}.";
        return false;
    }

    index += 1;
    value = args[index];
    error = null;
    return true;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  Glovelly.Worker calendar-sync drain [--max-items N] [--max-duration-seconds N] [--owner-id VALUE] [--processing-timeout-seconds N]");
}
