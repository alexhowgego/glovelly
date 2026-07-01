namespace Glovelly.Api.Services;

public sealed record ForScoreLibraryImportImpact(
    int CheckedSetListCount,
    int AffectedSetListCount,
    int CheckedItemCount,
    int AutoRelinkedItemCount,
    int NeedsReviewItemCount,
    IReadOnlyList<ForScoreLibraryImportImpactedSetList> SetLists);

public sealed record ForScoreLibraryImportImpactedSetList(
    Guid GigId,
    Guid SetListImportId,
    string GigTitle,
    DateOnly GigDate,
    string GigStatus,
    int AutoRelinkedItemCount,
    int NeedsReviewItemCount);

public sealed record ForScoreLibraryImportResult(
    Models.ForScoreLibrarySnapshot Snapshot,
    ForScoreLibraryImportImpact Impact);
