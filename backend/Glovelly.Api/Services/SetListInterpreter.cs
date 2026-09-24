using System.Text.Json;
using Glovelly.Api.Models;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using GenAiClient = Google.GenAI.Client;
using GenAiType = Google.GenAI.Types.Type;

namespace Glovelly.Api.Services;

public interface ISetListInterpreter
{
    Task<IReadOnlyList<SetListImportItemDraft>> InterpretAsync(GoogleSheetGrid grid, CancellationToken cancellationToken = default);
}

public sealed class VertexAiSetListInterpreter : ISetListInterpreter
{
    private readonly Func<string, List<Content>, GenerateContentConfig, CancellationToken, Task<GenerateContentResponse>> _generate;
    private readonly SetListInterpretationSettings _settings;

    public VertexAiSetListInterpreter(IOptions<SetListInterpretationSettings> options)
    {
        _settings = options.Value;
        var client = new GenAiClient(project: _settings.VertexAiProjectId, location: _settings.VertexAiLocation, enterprise: true);
        _generate = (model, contents, config, cancellationToken) => client.Models.GenerateContentAsync(model, contents, config, cancellationToken);
    }

    public async Task<IReadOnlyList<SetListImportItemDraft>> InterpretAsync(GoogleSheetGrid grid, CancellationToken cancellationToken = default)
    {
        if (!_settings.IsVertexAiConfigured) throw new InvalidOperationException("Set-list interpretation AI is not configured.");
        var prompt = JsonSerializer.Serialize(grid, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var response = await _generate(_settings.VertexAiModel!, [new Content { Role = "user", Parts = [new Part { Text = prompt }] }], new GenerateContentConfig
        {
            ResponseMimeType = "application/json", ResponseSchema = ResponseSchema,
            SystemInstruction = new Content { Parts = [new Part { Text = "Interpret this Google Sheet grid as a set list. Return only supported logical items in visual source order. Never invent songs. Do not promote vocalists, instruments, durations, keys, pads, or rehearsal notes to Song titles. Cite every item with its source cells." }] },
        }, cancellationToken);
        var text = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("The interpretation AI returned no result.");
        var output = JsonSerializer.Deserialize<InterpretationOutput>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The interpretation AI returned invalid JSON.");
        return SetListInterpretationValidator.Validate(output.Items, grid);
    }

    private static readonly Schema TextSchema = new() { Type = GenAiType.String, Nullable = false };
    private static readonly Schema NullableTextSchema = new() { Type = GenAiType.String, Nullable = true };

    private static readonly Schema ResponseSchema = new()
    {
        Type = GenAiType.Object, Nullable = false,
        Properties = new Dictionary<string, Schema>
        {
            ["items"] = new Schema
            {
                Type = GenAiType.Array, Nullable = false,
                Items = new Schema
                {
                    Type = GenAiType.Object, Nullable = false,
                    Properties = new Dictionary<string, Schema>
                    {
                        ["kind"] = TextSchema, ["include"] = new Schema { Type = GenAiType.Boolean, Nullable = false },
                        ["title"] = TextSchema, ["section"] = NullableTextSchema, ["padNumber"] = NullableTextSchema,
                        ["key"] = NullableTextSchema, ["notes"] = NullableTextSchema, ["confidence"] = TextSchema,
                        ["sourceCells"] = new Schema { Type = GenAiType.Array, Nullable = false, Items = TextSchema },
                    },
                },
            },
        },
    };

}

public sealed record InterpretationOutput(List<InterpretationItem> Items);
public sealed record InterpretationItem(string Kind, bool Include, string Title, string? Section, string? PadNumber, string? Key, string? Notes, string Confidence, List<string> SourceCells);

public static class SetListInterpretationValidator
{
    public static IReadOnlyList<SetListImportItemDraft> Validate(IReadOnlyList<InterpretationItem>? items, GoogleSheetGrid grid)
    {
        if (items is null || items.Count == 0) throw new InvalidOperationException("The interpretation did not contain any supported items.");
        var cells = grid.Cells.ToDictionary(cell => cell.Coordinate, StringComparer.OrdinalIgnoreCase);
        var result = new List<SetListImportItemDraft>();
        var previousRow = 0;
        foreach (var item in items)
        {
            if (!Enum.TryParse<GigSetListItemKind>(item.Kind, true, out var kind) || !Enum.IsDefined(kind)) throw new InvalidOperationException("The interpretation contained an unsupported item kind.");
            if (!Enum.TryParse<GigSetListItemConfidence>(item.Confidence, true, out var confidence) || !Enum.IsDefined(confidence)) throw new InvalidOperationException("The interpretation contained an invalid confidence.");
            if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 500 || item.SourceCells is null || item.SourceCells.Count == 0) throw new InvalidOperationException("The interpretation contained an invalid item.");
            var evidence = item.SourceCells.Distinct(StringComparer.OrdinalIgnoreCase).Select(coordinate => cells.TryGetValue(coordinate, out var cell) && !string.IsNullOrWhiteSpace(cell.DisplayValue) ? cell : throw new InvalidOperationException("The interpretation cited source evidence outside the grid.")).OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToList();
            if (evidence[0].Row < previousRow) throw new InvalidOperationException("The interpretation was not in source order.");
            previousRow = evidence[0].Row;
            result.Add(new SetListImportItemDraft(evidence[0].Row, result.Count, kind, item.Include && kind == GigSetListItemKind.Song, Trim(item.Section, 200), Trim(item.PadNumber, 100), Trim(item.Key, 100), item.Title.Trim(), Trim(item.Notes, 4000), JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonSerializerDefaults.Web)), confidence, null, null, Guid.NewGuid(), JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        }
        return result;
    }

    private static string? Trim(string? value, int maximum) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= maximum ? value.Trim() : throw new InvalidOperationException("The interpretation contained overlong metadata.");
}
