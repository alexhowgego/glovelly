using Glovelly.Api.Configuration;
using Glovelly.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddGlovellyWorkerInfrastructure(builder.Configuration, builder.Environment);

using var host = builder.Build();
var rootCommand = BuildRootCommand(host.Services);
return await rootCommand.Parse(args).InvokeAsync();

static RootCommand BuildRootCommand(IServiceProvider services)
{
    var maxItemsOption = new Option<int>("--max-items")
    {
        Description = "Maximum number of queued calendar sync items to process.",
        DefaultValueFactory = _ => 100
    };
    AddRequiredPositiveIntegerValidator(maxItemsOption);

    var maxDurationSecondsOption = new Option<int?>("--max-duration-seconds")
    {
        Description = "Maximum drain duration in seconds."
    };
    AddOptionalPositiveIntegerValidator(maxDurationSecondsOption);

    var ownerIdOption = new Option<string?>("--owner-id")
    {
        Description = "Processing owner ID to stamp on claimed work items."
    };

    var processingTimeoutSecondsOption = new Option<int?>("--processing-timeout-seconds")
    {
        Description = "Seconds before in-flight work is considered stale and recovered."
    };
    AddOptionalPositiveIntegerValidator(processingTimeoutSecondsOption);

    var drainCommand = new Command("drain", "Drain queued Google Calendar sync work items.");
    drainCommand.Options.Add(maxItemsOption);
    drainCommand.Options.Add(maxDurationSecondsOption);
    drainCommand.Options.Add(ownerIdOption);
    drainCommand.Options.Add(processingTimeoutSecondsOption);
    drainCommand.SetAction(async (parseResult, cancellationToken) =>
    {
        var maxDurationSeconds = parseResult.GetValue(maxDurationSecondsOption);
        var processingTimeoutSeconds = parseResult.GetValue(processingTimeoutSecondsOption);
        var options = new CalendarSyncDrainOptions(
            parseResult.GetValue(maxItemsOption),
            maxDurationSeconds is { } maxDuration ? TimeSpan.FromSeconds(maxDuration) : null,
            parseResult.GetValue(ownerIdOption),
            processingTimeoutSeconds is { } processingTimeout ? TimeSpan.FromSeconds(processingTimeout) : null);

        return await RunCalendarSyncDrainAsync(options, services, cancellationToken);
    });

    var calendarSyncCommand = new Command("calendar-sync", "Manage Google Calendar sync background work.");
    calendarSyncCommand.Subcommands.Add(drainCommand);

    var rootCommand = new RootCommand("Glovelly background worker commands.");
    rootCommand.Subcommands.Add(calendarSyncCommand);
    return rootCommand;
}

static async Task<int> RunCalendarSyncDrainAsync(
    CalendarSyncDrainOptions options,
    IServiceProvider services,
    CancellationToken cancellationToken)
{
    var logger = services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Glovelly.Worker.CalendarSync");
    var timeProvider = services.GetRequiredService<TimeProvider>();
    var scheduledTask = services.GetRequiredService<GoogleCalendarPropagationScheduledTask>();
    var context = new ScheduledTaskContext(timeProvider.GetUtcNow());
    var decision = await scheduledTask.ShouldRunAsync(context, cancellationToken);

    if (!decision.ShouldRun)
    {
        logger.LogInformation(
            "Scheduled task {TaskName} skipped: {Reason}",
            scheduledTask.Name,
            decision.Reason);
        return 0;
    }

    logger.LogInformation(
        "Scheduled task {TaskName} running: {Reason}",
        scheduledTask.Name,
        decision.Reason);

    var result = await scheduledTask.ExecuteAsync(options, context, cancellationToken);
    logger.LogInformation(
        "Calendar sync drain complete: {Processed} processed, {Succeeded} succeeded, {Retried} retried, {Failed} failed, {Skipped} skipped, {Recovered} recovered, completion reason {CompletionReason}.",
        result.Processed,
        result.Succeeded,
        result.Retried,
        result.Failed,
        result.Skipped,
        result.Recovered,
        result.CompletionReason);

    return 0;
}

static void AddRequiredPositiveIntegerValidator(Option<int> option)
{
    option.Validators.Add(result =>
    {
        if (result.GetValueOrDefault<int>() <= 0)
        {
            result.AddError($"{option.Name} must be a positive integer.");
        }
    });
}

static void AddOptionalPositiveIntegerValidator(Option<int?> option)
{
    option.Validators.Add(result =>
    {
        if (result.GetValueOrDefault<int?>() is { } value && value <= 0)
        {
            result.AddError($"{option.Name} must be a positive integer.");
        }
    });
}
