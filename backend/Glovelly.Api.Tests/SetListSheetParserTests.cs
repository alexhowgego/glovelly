using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class SetListSheetParserTests
{
    private readonly SetListSheetParser _parser = new();

    [Fact]
    public void Parse_BbbStyleRows_PreservesSongsAndSeparators()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Bella and the Bourbon Boys set list" },
            new[] { "Set One" },
            new[] { "61-E", "E", "LOVE", "40s swing", "Check key" },
            new[] { "1", "Bb min", "Upside Down", "40s swing", "" },
        };

        var items = _parser.Parse(rows);

        Assert.Contains(items, item => item.Kind == GigSetListItemKind.Separator && !item.Include && item.Title == "Set One");
        var songs = items.Where(item => item.Kind == GigSetListItemKind.Song).ToList();
        Assert.Equal(2, songs.Count);
        Assert.Equal("LOVE", songs[0].Title);
        Assert.Equal("61-E", songs[0].PadNumber);
        Assert.Equal("E", songs[0].Key);
        Assert.Equal(3, songs[0].SourceRowNumber);
    }

    [Fact]
    public void Parse_HeaderStyleRows_IgnoresInstructionRowsByDefault()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Down for the Count: Set List" },
            new[] { "", "Folder tidy-up: delete old parts" },
            new[] { "Pad #", "Key", "Song", "", "Vocalist", "Notes" },
            new[] { "Set One (60 mins)" },
            new[] { "74-G", "G", "L-O-V-E", "", "Callum", "" },
            new[] { "", "", "Please delete any old parts you may have!", "", "", "" },
        };

        var items = _parser.Parse(rows);

        Assert.Contains(items, item => item.Kind == GigSetListItemKind.Song && item.Title == "L-O-V-E" && item.Include);
        Assert.Contains(items, item => item.Kind == GigSetListItemKind.Comment && item.Title.Contains("delete", StringComparison.OrdinalIgnoreCase) && !item.Include);
    }

    [Fact]
    public void Parse_GuernseyStyleRows_UsesMusicalContextForTitleColumn()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "8.30pm", "", "DFTC set 1 (45 mins)" },
            new[] { "103", "", "Pennsylvania 6-5000", "Instrumental", "", "Ab", "Glenn Miller" },
            new[] { "", "", "Event starts, band on stage" },
        };

        var items = _parser.Parse(rows);

        var song = Assert.Single(items, item => item.Kind == GigSetListItemKind.Song);
        Assert.Equal("Pennsylvania 6-5000", song.Title);
        Assert.Equal("103", song.PadNumber);
        Assert.Contains(items, item => item.Kind == GigSetListItemKind.Comment && item.Title == "Event starts, band on stage" && !item.Include);
    }
}
