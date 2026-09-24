using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed record SetListImportItemDraft(
    int SourceRowNumber,
    int SortOrder,
    GigSetListItemKind Kind,
    bool Include,
    string? Section,
    string? PadNumber,
    string? Key,
    string Title,
    string? Notes,
    string RawCellsJson,
    GigSetListItemConfidence Confidence,
    Guid? ForScoreChartId = null,
    SetListChartMatchResult? ForScoreMatch = null,
    Guid? ItemId = null,
    string? SourceEvidenceJson = null);
