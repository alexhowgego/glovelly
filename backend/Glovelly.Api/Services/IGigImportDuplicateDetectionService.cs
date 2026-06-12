using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IGigImportDuplicateDetectionService
{
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> FindWarningsAsync(
        Guid? userId,
        GigImportBatch batch,
        IReadOnlyList<GigImportDraft> drafts,
        CancellationToken cancellationToken = default);
}
