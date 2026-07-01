using System.Text.Json;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface ISetListSheetParser
{
    IReadOnlyList<SetListImportItemDraft> Parse(IReadOnlyList<IReadOnlyList<string>> rows);
}

public sealed class SetListSheetParser : ISetListSheetParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<SetListImportItemDraft> Parse(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var drafts = new List<SetListImportItemDraft>();
        var header = SheetHeader.Detect(rows);
        var currentSection = (string?)null;
        var sortOrder = 0;

        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = index + 1;
            var cells = NormalizeCells(rows[index]);
            if (cells.Count == 0 || cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (header is not null && rowNumber == header.RowNumber)
            {
                continue;
            }

            var nonEmptyCells = cells.Where(cell => !string.IsNullOrWhiteSpace(cell)).ToList();
            var firstCell = nonEmptyCells.FirstOrDefault() ?? string.Empty;
            if (LooksLikeSectionRow(cells, firstCell))
            {
                currentSection = firstCell;
                drafts.Add(CreateDraft(
                    rowNumber,
                    sortOrder++,
                    GigSetListItemKind.Separator,
                    include: false,
                    currentSection,
                    null,
                    null,
                    firstCell,
                    null,
                    cells,
                    GigSetListItemConfidence.High));
                continue;
            }

            var parsed = header is not null && rowNumber > header.RowNumber
                ? ParseWithHeader(cells, header, currentSection)
                : ParseWithoutHeader(cells, currentSection);

            if (parsed is null)
            {
                continue;
            }

            drafts.Add(parsed with
            {
                SourceRowNumber = rowNumber,
                SortOrder = sortOrder++,
                RawCellsJson = JsonSerializer.Serialize(cells, JsonOptions),
            });
        }

        return drafts;
    }

    private static SetListImportItemDraft? ParseWithHeader(
        IReadOnlyList<string> cells,
        SheetHeader header,
        string? currentSection)
    {
        var title = GetCell(cells, header.TitleColumn);
        var pad = GetCell(cells, header.PadColumn);
        var key = GetCell(cells, header.KeyColumn);
        var notes = JoinNotes(cells, header.NoteColumns);

        if (string.IsNullOrWhiteSpace(title))
        {
            return CreateComment(cells, currentSection);
        }

        if (!LooksLikeSong(title, pad, key, notes, cells))
        {
            return CreateComment(cells, currentSection, title);
        }

        return CreateDraft(
            0,
            0,
            GigSetListItemKind.Song,
            include: true,
            currentSection,
            NormalizeOptional(pad),
            NormalizeOptional(key),
            title.Trim(),
            NormalizeOptional(notes),
            cells,
            string.IsNullOrWhiteSpace(pad) && string.IsNullOrWhiteSpace(key)
                ? GigSetListItemConfidence.Medium
                : GigSetListItemConfidence.High);
    }

    private static SetListImportItemDraft? ParseWithoutHeader(IReadOnlyList<string> cells, string? currentSection)
    {
        var first = GetCell(cells, 0);
        var second = GetCell(cells, 1);
        var third = GetCell(cells, 2);

        var titleColumn = -1;
        var padColumn = -1;
        var keyColumn = -1;
        if (LooksLikePadNumber(first) && LooksLikeKey(second) && !string.IsNullOrWhiteSpace(third))
        {
            padColumn = 0;
            keyColumn = 1;
            titleColumn = 2;
        }
        else if (LooksLikePadNumber(first) && !string.IsNullOrWhiteSpace(second))
        {
            padColumn = 0;
            titleColumn = 1;
        }
        else if (!string.IsNullOrWhiteSpace(third) && RowHasMusicalContext(cells))
        {
            titleColumn = 2;
            keyColumn = LooksLikeKey(GetCell(cells, 5)) ? 5 : -1;
            padColumn = LooksLikePadNumber(first) ? 0 : -1;
        }
        else if (!string.IsNullOrWhiteSpace(first) && cells.Count == 1)
        {
            return CreateComment(cells, currentSection, first);
        }

        if (titleColumn < 0)
        {
            return CreateComment(cells, currentSection);
        }

        var title = GetCell(cells, titleColumn);
        var pad = GetCell(cells, padColumn);
        var key = GetCell(cells, keyColumn);
        var notes = JoinRemainingCells(cells, new HashSet<int> { padColumn, keyColumn, titleColumn });
        if (!LooksLikeSong(title, pad, key, notes, cells))
        {
            return CreateComment(cells, currentSection, title);
        }

        return CreateDraft(
            0,
            0,
            GigSetListItemKind.Song,
            include: true,
            currentSection,
            NormalizeOptional(pad),
            NormalizeOptional(key),
            title.Trim(),
            NormalizeOptional(notes),
            cells,
            string.IsNullOrWhiteSpace(pad) && string.IsNullOrWhiteSpace(key)
                ? GigSetListItemConfidence.Medium
                : GigSetListItemConfidence.High);
    }

    private static SetListImportItemDraft? CreateComment(IReadOnlyList<string> cells, string? currentSection, string? title = null)
    {
        var display = title?.Trim();
        if (string.IsNullOrWhiteSpace(display))
        {
            display = string.Join(" ", cells.Where(cell => !string.IsNullOrWhiteSpace(cell))).Trim();
        }

        if (string.IsNullOrWhiteSpace(display))
        {
            return null;
        }

        return CreateDraft(
            0,
            0,
            GigSetListItemKind.Comment,
            include: false,
            currentSection,
            null,
            null,
            display,
            null,
            cells,
            GigSetListItemConfidence.Low);
    }

    private static SetListImportItemDraft CreateDraft(
        int rowNumber,
        int sortOrder,
        GigSetListItemKind kind,
        bool include,
        string? section,
        string? padNumber,
        string? key,
        string title,
        string? notes,
        IReadOnlyList<string> cells,
        GigSetListItemConfidence confidence)
    {
        return new SetListImportItemDraft(
            rowNumber,
            sortOrder,
            kind,
            include,
            NormalizeOptional(section),
            padNumber,
            key,
            title,
            notes,
            JsonSerializer.Serialize(cells, JsonOptions),
            confidence);
    }

    private static List<string> NormalizeCells(IReadOnlyList<string> cells)
    {
        var normalized = cells.Select(cell => cell.Trim()).ToList();
        for (var i = normalized.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(normalized[i]))
            {
                return normalized.Take(i + 1).ToList();
            }
        }

        return [];
    }

    private static string GetCell(IReadOnlyList<string> cells, int column)
    {
        return column >= 0 && column < cells.Count ? cells[column].Trim() : string.Empty;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? JoinNotes(IReadOnlyList<string> cells, IReadOnlyList<int> noteColumns)
    {
        return NormalizeOptional(string.Join(" | ", noteColumns.Select(column => GetCell(cells, column)).Where(value => !string.IsNullOrWhiteSpace(value))));
    }

    private static string? JoinRemainingCells(IReadOnlyList<string> cells, IReadOnlySet<int> excludedColumns)
    {
        return NormalizeOptional(string.Join(" | ", cells
            .Select((cell, index) => excludedColumns.Contains(index) ? string.Empty : cell)
            .Where(value => !string.IsNullOrWhiteSpace(value))));
    }

    private static bool LooksLikeSectionRow(IReadOnlyList<string> cells, string firstCell)
    {
        var nonEmptyCount = cells.Count(cell => !string.IsNullOrWhiteSpace(cell));
        if (nonEmptyCount > 2 || string.IsNullOrWhiteSpace(firstCell))
        {
            return false;
        }

        return firstCell.Contains("set", StringComparison.OrdinalIgnoreCase)
            || firstCell.Contains("mins", StringComparison.OrdinalIgnoreCase)
            || firstCell.Contains("performance", StringComparison.OrdinalIgnoreCase)
            || firstCell.EndsWith(':');
    }

    private static bool LooksLikeSong(string title, string? pad, string? key, string? notes, IReadOnlyList<string> cells)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 2)
        {
            return false;
        }

        if (LooksLikeInstruction(title)
            || LooksLikeTimeOnly(title)
            || (title.Contains("set", StringComparison.OrdinalIgnoreCase) && title.Contains("min", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return LooksLikePadNumber(pad)
            || LooksLikeKey(key)
            || RowHasMusicalContext(cells)
            || (!string.IsNullOrWhiteSpace(notes) && !LooksLikeInstruction(notes));
    }

    private static bool RowHasMusicalContext(IReadOnlyList<string> cells)
    {
        return cells.Any(cell => LooksLikePadNumber(cell))
            || cells.Any(cell => LooksLikeKey(cell))
            || cells.Any(cell => cell.Contains("vocal", StringComparison.OrdinalIgnoreCase))
            || cells.Any(cell => cell.Contains("instrumental", StringComparison.OrdinalIgnoreCase))
            || cells.Any(cell => cell.Contains("motown", StringComparison.OrdinalIgnoreCase))
            || cells.Any(cell => cell.Contains("swing", StringComparison.OrdinalIgnoreCase))
            || cells.Any(cell => cell.Contains("charleston", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeInstruction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("download", StringComparison.OrdinalIgnoreCase)
            || value.Contains("delete", StringComparison.OrdinalIgnoreCase)
            || value.Contains("folder", StringComparison.OrdinalIgnoreCase)
            || value.Contains("speech", StringComparison.OrdinalIgnoreCase)
            || value.Contains("doors open", StringComparison.OrdinalIgnoreCase)
            || value.Contains("event starts", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeTimeOnly(string value)
    {
        return TimeOnly.TryParse(value, out _);
    }

    private static bool LooksLikePadNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains("am", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("pm", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return trimmed.Any(char.IsDigit) && trimmed.Length <= 20;
    }

    private static bool LooksLikeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("min", "m", StringComparison.OrdinalIgnoreCase);
        return normalized.Length <= 8
            && "ABCDEFG".Contains(char.ToUpperInvariant(normalized[0]), StringComparison.Ordinal)
            && normalized.Skip(1).All(character => character is '#' or 'b' or 'm' or '/' || "ABCDEFG".Contains(char.ToUpperInvariant(character), StringComparison.Ordinal));
    }

    private sealed record SheetHeader(int RowNumber, int TitleColumn, int PadColumn, int KeyColumn, IReadOnlyList<int> NoteColumns)
    {
        public static SheetHeader? Detect(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            for (var rowIndex = 0; rowIndex < Math.Min(rows.Count, 25); rowIndex++)
            {
                var cells = rows[rowIndex].Select(cell => cell.Trim().ToLowerInvariant()).ToList();
                var titleColumn = cells.FindIndex(cell => cell is "song" or "title" or "song title");
                if (titleColumn < 0)
                {
                    continue;
                }

                var padColumn = cells.FindIndex(cell => cell.Contains("pad", StringComparison.OrdinalIgnoreCase) || cell.Contains("chart", StringComparison.OrdinalIgnoreCase));
                var keyColumn = cells.FindIndex(cell => string.Equals(cell, "key", StringComparison.OrdinalIgnoreCase));
                var noteColumns = cells
                    .Select((cell, index) => (cell, index))
                    .Where(value => value.cell.Contains("note", StringComparison.OrdinalIgnoreCase)
                        || value.cell.Contains("parts", StringComparison.OrdinalIgnoreCase)
                        || value.cell.Contains("vocal", StringComparison.OrdinalIgnoreCase))
                    .Select(value => value.index)
                    .Where(index => index != titleColumn && index != padColumn && index != keyColumn)
                    .ToList();

                return new SheetHeader(rowIndex + 1, titleColumn, padColumn, keyColumn, noteColumns);
            }

            return null;
        }
    }
}

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
    SetListChartMatchResult? ForScoreMatch = null);
