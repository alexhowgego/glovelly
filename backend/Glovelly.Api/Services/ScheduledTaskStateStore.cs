using Google;
using System.Text.Json;

namespace Glovelly.Api.Services;

public interface IScheduledTaskStateStore
{
    Task<ScheduledTaskStateEnvelope<TState>?> ReadAsync<TState>(
        string taskName,
        CancellationToken cancellationToken = default)
        where TState : class, new();

    Task WriteAsync<TState>(
        ScheduledTaskStateEnvelope<TState> envelope,
        CancellationToken cancellationToken = default)
        where TState : class, new();
}

public sealed class BlobScheduledTaskStateStore(IBlobStore blobStore) : IScheduledTaskStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<ScheduledTaskStateEnvelope<TState>?> ReadAsync<TState>(
        string taskName,
        CancellationToken cancellationToken = default)
        where TState : class, new()
    {
        try
        {
            await using var content = (await blobStore.OpenReadAsync(BuildKey(taskName), cancellationToken)).Content;
            return await JsonSerializer.DeserializeAsync<ScheduledTaskStateEnvelope<TState>>(
                content,
                JsonOptions,
                cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (GoogleApiException exception) when (GoogleApiExceptionSupport.IsNotFound(exception))
        {
            return null;
        }
    }

    public async Task WriteAsync<TState>(
        ScheduledTaskStateEnvelope<TState> envelope,
        CancellationToken cancellationToken = default)
        where TState : class, new()
    {
        ArgumentNullException.ThrowIfNull(envelope);

        await using var content = new MemoryStream();
        await JsonSerializer.SerializeAsync(content, envelope, JsonOptions, cancellationToken);
        content.Position = 0;
        await blobStore.SaveAsync(
            new BlobWriteRequest(BuildKey(envelope.TaskName), content, "application/json", content.Length),
            cancellationToken);
    }

    private static string BuildKey(string taskName)
    {
        return $"worker/scheduled-tasks/{taskName}.json";
    }
}

public sealed class ScheduledTaskStateEnvelope<TState>
    where TState : class, new()
{
    public int SchemaVersion { get; set; } = 1;

    public string TaskName { get; set; } = string.Empty;

    public DateTimeOffset? LastDecisionUtc { get; set; }

    public DateTimeOffset? LastSuccessfulRunUtc { get; set; }

    public TState State { get; set; } = new();
}
