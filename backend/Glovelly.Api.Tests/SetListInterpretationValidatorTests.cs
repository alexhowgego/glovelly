using System.Text.Json;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class SetListInterpretationValidatorTests
{
    [Fact]
    public void Validate_DetailedMedleyDraftRetainsTwentySixSongsMedleysTransitionAndSpares()
    {
        var grid = LoadGrid("detailed-medley-grid.json");
        var items = SetListInterpretationValidator.Validate(
        [
            new("Separator", false, "Midnight Medley", null, "150", null, null, "High", ["B4", "D4"]),
            new("Song", true, "Silver Steps", "Midnight Medley", null, null, null, "High", ["E5"]),
            new("Song", true, "Open Window", "Midnight Medley", null, null, null, "High", ["E6"]),
            new("Song", true, "Northern Lights", "Midnight Medley", null, null, null, "High", ["E7"]),
            new("Song", true, "Paper Planes", "Midnight Medley", null, null, null, "High", ["E8"]),
            new("Song", true, "Signal Fire", null, "22-D", "D", null, "High", ["B9", "C9", "D9"]),
            new("Song", true, "Afterglow", null, null, null, null, "High", ["D10"]),
            new("Song", true, "Velvet Sky", null, null, null, null, "High", ["D11"]),
            new("Song", true, "Turning Tide", null, null, null, null, "High", ["D12"]),
            new("Song", true, "Little Lantern", null, null, null, null, "High", ["D13"]),
            new("Song", true, "Blue Hour", null, null, null, null, "High", ["D14"]),
            new("Song", true, "Golden Thread", null, null, null, null, "High", ["D15"]),
            new("Song", true, "Back to Shore", null, null, null, null, "High", ["D16"]),
            new("Song", true, "Bright Side", null, null, null, null, "High", ["D17"]),
            new("Song", true, "Wild River", null, null, null, null, "High", ["D18"]),
            new("Separator", false, "Harbour Suite", null, null, null, null, "High", ["D19"]),
            new("Song", true, "Running Free", "Harbour Suite", null, null, null, "High", ["E20"]),
            new("Song", true, "First Light", "Harbour Suite", null, null, null, "High", ["E21"]),
            new("Song", true, "City Spark", "Harbour Suite", null, null, null, "High", ["E22"]),
            new("Song", true, "Last Call", null, null, null, null, "High", ["D23"]),
            new("Song", true, "Day Shift", null, null, null, null, "High", ["D24"]),
            new("Song", true, "Keep Moving", null, null, null, null, "High", ["D25"]),
            new("Song", true, "River Road", null, null, null, null, "High", ["D26"]),
            new("Song", true, "Quiet Storm", null, null, null, null, "High", ["D27"]),
            new("Transition", false, "segue into", null, null, null, null, "Medium", ["E28"]),
            new("Song", true, "High Water", null, null, null, null, "High", ["D29"]),
            new("Song", true, "Morning Train", null, null, null, null, "High", ["D30"]),
            new("Song", true, "As - 2019 version", null, null, null, null, "Medium", ["D31"]),
            new("Separator", false, "SPARES", null, null, null, null, "High", ["A32"]),
            new("Song", false, "Midnight Echo", "SPARES", null, null, null, "Medium", ["D32"]),
        ], grid);

        Assert.Equal(26, items.Count(item => item.Kind == GigSetListItemKind.Song));
        Assert.Contains(items, item => item.Kind == GigSetListItemKind.Transition && item.Title == "segue into");
        Assert.Contains(items, item => item.Section == "Midnight Medley" && item.SourceEvidenceJson?.Contains("E5", StringComparison.Ordinal) == true);
        Assert.Contains(items, item => item.Section == "SPARES" && !item.Include);
    }

    [Theory]
    [InlineData("detailed-medley-grid.json")]
    [InlineData("simple-band-grid.json")]
    [InlineData("varied-layout-grid.json")]
    public void GridFixtures_AreCoordinatePreservingAndSanitized(string fixtureName)
    {
        var grid = LoadGrid(fixtureName);

        Assert.NotEmpty(grid.Cells);
        Assert.All(grid.Cells, cell =>
        {
            Assert.InRange(cell.Row, 1, grid.RowCount);
            Assert.InRange(cell.Column, 1, grid.ColumnCount);
            Assert.False(string.IsNullOrWhiteSpace(cell.Coordinate));
            Assert.False(string.IsNullOrWhiteSpace(cell.DisplayValue));
        });
    }

    [Fact]
    public void Validate_RetainsMedleyEvidenceAndExcludedItems()
    {
        var grid = new GoogleSheetGrid("Synthetic programme", 6, 5,
        [
            new(1, 2, "B1", "150"), new(1, 4, "D1", "Midnight Medley"),
            new(2, 5, "E2", "Silver Steps"), new(3, 5, "E3", "Northern Lights"),
            new(4, 1, "A4", "SPARES (probably won't play!)"), new(5, 4, "D5", "How Sweet It Is"),
        ]);

        var items = SetListInterpretationValidator.Validate(
        [
            new("Separator", false, "Midnight Medley", null, "150", null, null, "High", ["B1", "D1"]),
            new("Song", true, "Silver Steps", "Midnight Medley", null, null, null, "High", ["E2"]),
            new("Song", true, "Northern Lights", "Midnight Medley", null, null, null, "High", ["E3"]),
            new("Separator", false, "SPARES (probably won't play!)", null, null, null, null, "High", ["A4"]),
            new("Song", false, "How Sweet It Is", "SPARES (probably won't play!)", null, null, null, "Medium", ["D5"]),
        ], grid);

        Assert.Equal(5, items.Count);
        Assert.Equal(1, items[0].SourceRowNumber);
        Assert.Contains("D1", items[0].SourceEvidenceJson, StringComparison.Ordinal);
        Assert.All(items.Where(item => item.Section?.Contains("SPARES", StringComparison.Ordinal) == true), item => Assert.False(item.Include));
    }

    [Fact]
    public void Validate_RejectsUnknownOrBlankSourceEvidence()
    {
        var grid = new GoogleSheetGrid("Set list", 1, 1, [new(1, 1, "A1", "Song")]);

        Assert.Throws<InvalidOperationException>(() => SetListInterpretationValidator.Validate(
        [
            new("Song", true, "Song", null, null, null, null, "High", ["B1"]),
        ], grid));
    }

    [Fact]
    public void Validate_RejectsOutOfOrderItems()
    {
        var grid = new GoogleSheetGrid("Set list", 2, 1, [new(1, 1, "A1", "First"), new(2, 1, "A2", "Second")]);

        Assert.Throws<InvalidOperationException>(() => SetListInterpretationValidator.Validate(
        [
            new("Song", true, "Second", null, null, null, null, "High", ["A2"]),
            new("Song", true, "First", null, null, null, null, "High", ["A1"]),
        ], grid));
    }

    [Fact]
    public void Validate_RejectsInvalidSchemaLengthsAndEvidence()
    {
        var grid = new GoogleSheetGrid("Set list", 1, 1, [new(1, 1, "A1", "Song")]);

        Assert.Throws<InvalidOperationException>(() => SetListInterpretationValidator.Validate(
        [new("Unsupported", true, "Song", null, null, null, null, "High", ["A1"])], grid));
        Assert.Throws<InvalidOperationException>(() => SetListInterpretationValidator.Validate(
        [new("Song", true, "Song", null, null, null, null, "Certain", ["A1"])], grid));
        Assert.Throws<InvalidOperationException>(() => SetListInterpretationValidator.Validate(
        [new("Song", true, new string('x', 501), null, null, null, null, "High", ["A1"])], grid));
        Assert.Throws<InvalidOperationException>(() => SetListInterpretationValidator.Validate(
        [new("Song", true, "Song", null, null, null, new string('x', 4001), "High", ["A1"])], grid));
        Assert.Throws<InvalidOperationException>(() => SetListInterpretationValidator.Validate(
        [new("Song", true, "Song", null, null, null, null, "High", ["A2"])], grid));
    }

    private static GoogleSheetGrid LoadGrid(string fixtureName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SetLists", fixtureName);
        return JsonSerializer.Deserialize<GoogleSheetGrid>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Fixture {fixtureName} could not be read.");
    }
}
