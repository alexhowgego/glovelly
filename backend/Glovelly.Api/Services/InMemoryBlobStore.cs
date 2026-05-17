using System.Collections.Concurrent;

namespace Glovelly.Api.Services;

public sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, StoredBlob> _blobs = new();

    public async Task SaveAsync(
        BlobWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var memory = new MemoryStream();
        await request.Content.CopyToAsync(memory, cancellationToken);
        _blobs[request.Key] = new StoredBlob(
            memory.ToArray(),
            request.ContentType,
            request.SizeBytes ?? memory.Length);
    }

    public Task<BlobReadResult> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (!_blobs.TryGetValue(key, out var blob))
        {
            throw new FileNotFoundException("Blob was not found.", key);
        }

        return Task.FromResult(new BlobReadResult(
            new MemoryStream(blob.Content, writable: false),
            blob.ContentType,
            blob.SizeBytes));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _blobs.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private sealed record StoredBlob(byte[] Content, string ContentType, long SizeBytes);
}
