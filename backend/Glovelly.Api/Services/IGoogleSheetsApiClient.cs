namespace Glovelly.Api.Services;

public interface IGoogleSheetsApiClient
{
    Task<GoogleSpreadsheetMetadata> GetSpreadsheetMetadataAsync(
        GoogleConnectionAccessToken accessToken,
        string spreadsheetId,
        CancellationToken cancellationToken);

    Task<GoogleSheetValues> GetWorksheetValuesAsync(
        GoogleConnectionAccessToken accessToken,
        string spreadsheetId,
        string worksheetName,
        CancellationToken cancellationToken);

    Task<GoogleSheetGrid> GetWorksheetGridAsync(
        GoogleConnectionAccessToken accessToken,
        string spreadsheetId,
        string worksheetName,
        int maxRows,
        int maxColumns,
        CancellationToken cancellationToken) => throw new NotSupportedException("Coordinate-preserving worksheet grids are not available.");
}

public sealed record GoogleSpreadsheetMetadata(string SpreadsheetId, IReadOnlyList<GoogleSheetMetadata> Sheets);

public sealed record GoogleSheetMetadata(string SheetId, string Title, int Index);

public sealed record GoogleSheetValues(string Range, IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record GoogleSheetGrid(string WorksheetName, int RowCount, int ColumnCount, IReadOnlyList<GoogleSheetGridCell> Cells);

public sealed record GoogleSheetGridCell(int Row, int Column, string Coordinate, string DisplayValue);
