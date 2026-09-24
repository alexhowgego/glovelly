using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Glovelly.Api.Services;

public sealed class GoogleSheetsApiClient(HttpClient httpClient) : IGoogleSheetsApiClient
{
    private const string GoogleSheetsEndpoint = "https://sheets.googleapis.com/v4/spreadsheets";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GoogleSpreadsheetMetadata> GetSpreadsheetMetadataAsync(
        GoogleConnectionAccessToken accessToken,
        string spreadsheetId,
        CancellationToken cancellationToken)
    {
        var url = QueryHelpers.AddQueryString(
            $"{GoogleSheetsEndpoint}/{Uri.EscapeDataString(spreadsheetId)}",
            new Dictionary<string, string?>
            {
                ["fields"] = "spreadsheetId,sheets.properties(sheetId,title,index)",
            });

        using var request = CreateRequest(accessToken, url);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google Sheets metadata read failed with HTTP {(int)response.StatusCode}. {responseBody}".Trim());
        }

        var payload = JsonSerializer.Deserialize<SpreadsheetMetadataResponse>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("Google Sheets metadata response could not be parsed.");

        return new GoogleSpreadsheetMetadata(
            payload.SpreadsheetId ?? spreadsheetId,
            payload.Sheets
                .Select(sheet => sheet.Properties)
                .Where(properties => properties is not null && !string.IsNullOrWhiteSpace(properties.Title))
                .Select(properties => new GoogleSheetMetadata(
                    properties!.SheetId.ToString(),
                    properties.Title!,
                    properties.Index))
                .OrderBy(sheet => sheet.Index)
                .ToList());
    }

    public async Task<GoogleSheetValues> GetWorksheetValuesAsync(
        GoogleConnectionAccessToken accessToken,
        string spreadsheetId,
        string worksheetName,
        CancellationToken cancellationToken)
    {
        var escapedRange = Uri.EscapeDataString($"'{worksheetName.Replace("'", "''")}'");
        var url = QueryHelpers.AddQueryString(
            $"{GoogleSheetsEndpoint}/{Uri.EscapeDataString(spreadsheetId)}/values/{escapedRange}",
            new Dictionary<string, string?>
            {
                ["majorDimension"] = "ROWS",
                ["valueRenderOption"] = "FORMATTED_VALUE",
            });

        using var request = CreateRequest(accessToken, url);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google Sheets values read failed with HTTP {(int)response.StatusCode}. {responseBody}".Trim());
        }

        var payload = JsonSerializer.Deserialize<SheetValuesResponse>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("Google Sheets values response could not be parsed.");

        return new GoogleSheetValues(
            payload.Range ?? worksheetName,
            payload.Values.Select(row => row.Select(value => value?.Trim() ?? string.Empty).ToList()).ToList());
    }

    public async Task<GoogleSheetGrid> GetWorksheetGridAsync(
        GoogleConnectionAccessToken accessToken,
        string spreadsheetId,
        string worksheetName,
        int maxRows,
        int maxColumns,
        CancellationToken cancellationToken)
    {
        var url = QueryHelpers.AddQueryString(
            $"{GoogleSheetsEndpoint}/{Uri.EscapeDataString(spreadsheetId)}",
            new Dictionary<string, string?>
            {
                ["includeGridData"] = "true",
                ["ranges"] = $"'{worksheetName.Replace("'", "''")}'!A1:{ColumnName(maxColumns)}{maxRows}",
                ["fields"] = "sheets(properties(sheetId,title),data(startRow,startColumn,rowData(values(formattedValue))))",
            });

        using var request = CreateRequest(accessToken, url);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google Sheets grid read failed with HTTP {(int)response.StatusCode}. {responseBody}".Trim());
        }

        var payload = JsonSerializer.Deserialize<SpreadsheetGridResponse>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("Google Sheets grid response could not be parsed.");
        var sheet = payload.Sheets.FirstOrDefault(sheet => string.Equals(sheet.Properties?.Title, worksheetName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The selected worksheet was not returned by Google Sheets.");
        var data = sheet.Data.FirstOrDefault();
        var rows = data?.RowData ?? [];
        var rowCount = rows.Count;
        var columnCount = rows.Count == 0 ? 0 : rows.Max(row => row.Values.Count);
        var cells = new List<GoogleSheetGridCell>(rowCount * columnCount);
        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                var display = column < rows[row].Values.Count ? rows[row].Values[column].FormattedValue ?? string.Empty : string.Empty;
                cells.Add(new GoogleSheetGridCell(row + 1, column + 1, $"{ColumnName(column + 1)}{row + 1}", display));
            }
        }

        return new GoogleSheetGrid(worksheetName, rowCount, columnCount, cells);
    }

    private static HttpRequestMessage CreateRequest(GoogleConnectionAccessToken accessToken, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(accessToken.TokenType, accessToken.AccessToken);
        return request;
    }

    private sealed class SpreadsheetMetadataResponse
    {
        public string? SpreadsheetId { get; set; }
        public List<SheetResponse> Sheets { get; set; } = [];
    }

    private sealed class SheetResponse
    {
        public SheetPropertiesResponse? Properties { get; set; }
    }

    private sealed class SheetPropertiesResponse
    {
        public int SheetId { get; set; }
        public string? Title { get; set; }
        public int Index { get; set; }
    }

    private sealed class SheetValuesResponse
    {
        public string? Range { get; set; }
        public List<List<string?>> Values { get; set; } = [];
    }

    private static string ColumnName(int column)
    {
        var value = string.Empty;
        while (column > 0)
        {
            column--;
            value = (char)('A' + (column % 26)) + value;
            column /= 26;
        }

        return value;
    }

    private sealed class SpreadsheetGridResponse { public List<SheetGridResponse> Sheets { get; set; } = []; }
    private sealed class SheetGridResponse { public SheetPropertiesResponse? Properties { get; set; } public List<GridDataResponse> Data { get; set; } = []; }
    private sealed class GridDataResponse { public List<RowDataResponse> RowData { get; set; } = []; }
    private sealed class RowDataResponse { public List<CellDataResponse> Values { get; set; } = []; }
    private sealed class CellDataResponse { public string? FormattedValue { get; set; } }
}
