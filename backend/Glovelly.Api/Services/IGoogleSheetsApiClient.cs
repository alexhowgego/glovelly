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
}

public sealed record GoogleSpreadsheetMetadata(string SpreadsheetId, IReadOnlyList<GoogleSheetMetadata> Sheets);

public sealed record GoogleSheetMetadata(string SheetId, string Title, int Index);

public sealed record GoogleSheetValues(string Range, IReadOnlyList<IReadOnlyList<string>> Rows);
