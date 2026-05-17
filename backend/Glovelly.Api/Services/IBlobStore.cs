namespace Glovelly.Api.Services;

public sealed record BlobWriteRequest(
    string Key,
    Stream Content,
    string ContentType,
    long? SizeBytes = null);

public sealed record BlobReadResult(
    Stream Content,
    string? ContentType,
    long? SizeBytes);

public interface IBlobStore
{
    Task SaveAsync(BlobWriteRequest request, CancellationToken cancellationToken = default);
    Task<BlobReadResult> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
