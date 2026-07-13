using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Glovelly.Api.Services;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class ForScoreLibraryParserTests
{
    [Fact]
    public async Task ParseAsync_ExtractsChartMetadata()
    {
        var backup = ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Song One.pdf|title"] = "Song One",
            ["Song One.pdf|keywords"] = "Band A",
            ["Song One.pdf|printNumber"] = 2,
            ["Song One.pdf|version"] = 1,
            ["&SET;Ignored"] = "Song One",
        }, wrapperPrefix: "<--4SBV02--> variable wrapper ");
        var parser = new ForScoreLibraryParser();

        var result = await parser.ParseAsync(new MemoryStream(backup), TestContext.Current.CancellationToken);

        var chart = Assert.Single(result.Charts);
        Assert.Equal("V02", result.BackupVersion);
        Assert.Equal("Song One.pdf", chart.FilePath);
        Assert.Equal("Song One", chart.Title);
        Assert.Equal("SONG ONE", chart.NormalizedTitle);
        Assert.Equal("Band A", chart.Keywords);
        Assert.Equal(2, chart.PrintNumber);
        Assert.Equal(1, chart.Version);
    }

    [Fact]
    public async Task ParseAsync_LocatesGzipPayloadAtVariableOffset()
    {
        var backup = ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Offset Song.pdf|title"] = "Offset Song",
        }, wrapperPrefix: "custom-prefix-with-different-length");
        var parser = new ForScoreLibraryParser();

        var result = await parser.ParseAsync(new MemoryStream(backup), TestContext.Current.CancellationToken);

        Assert.Equal("Offset Song", Assert.Single(result.Charts).Title);
    }

    [Fact]
    public async Task ParseAsync_IgnoresTrailingBytesAfterGzipPayload()
    {
        var backup = ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Trailing.pdf|title"] = "Trailing",
        }).Concat("trailing-garbage"u8.ToArray()).ToArray();
        var parser = new ForScoreLibraryParser();

        var result = await parser.ParseAsync(new MemoryStream(backup), TestContext.Current.CancellationToken);

        Assert.Equal("Trailing", Assert.Single(result.Charts).Title);
    }

    [Fact]
    public async Task ParseAsync_DoesNotOverflowOnHighBitEightByteIntegers()
    {
        var backup = ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["High Bit.pdf|title"] = "High Bit",
            ["High Bit.pdf|printNumber"] = new RawPlistInteger(0x8000000000000001),
        });
        var parser = new ForScoreLibraryParser();

        var result = await parser.ParseAsync(new MemoryStream(backup), TestContext.Current.CancellationToken);

        var chart = Assert.Single(result.Charts);
        Assert.Equal("High Bit", chart.Title);
        Assert.Null(chart.PrintNumber);
    }

    [Fact]
    public async Task ParseAsync_InvalidFileThrowsParseException()
    {
        var parser = new ForScoreLibraryParser();

        await Assert.ThrowsAsync<ForScoreLibraryParseException>(() =>
            parser.ParseAsync(new MemoryStream("not a backup"u8.ToArray()), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ParseAsync_SkipsIncompleteChartMetadataWithWarning()
    {
        var backup = ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Complete.pdf|title"] = "Complete",
            ["Incomplete.pdf|keywords"] = "No title",
        });
        var parser = new ForScoreLibraryParser();

        var result = await parser.ParseAsync(new MemoryStream(backup), TestContext.Current.CancellationToken);

        Assert.Equal("Complete", Assert.Single(result.Charts).Title);
        Assert.Contains(result.Warnings, warning => warning.Contains("Incomplete.pdf", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_IgnoresPageSpecificAnnotationKeys()
    {
        var backup = ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Complete.pdf|title"] = "Complete",
            ["Complete.pdf|1|offset"] = "{0, 0}",
        });
        var parser = new ForScoreLibraryParser();

        var result = await parser.ParseAsync(new MemoryStream(backup), TestContext.Current.CancellationToken);

        Assert.Equal("Complete", Assert.Single(result.Charts).Title);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ParseAsync_PreservesCaseDistinctFilePaths()
    {
        var backup = ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Case.pdf|title"] = "Upper Case",
            ["case.pdf|title"] = "Lower Case",
        });
        var parser = new ForScoreLibraryParser();

        var result = await parser.ParseAsync(new MemoryStream(backup), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Charts.Count);
    }
}

internal static class ForScoreBackupFixture
{
    public static byte[] Build(Dictionary<string, object> metadata, string wrapperPrefix = "<--4SBV02--> test wrapper")
    {
        var plist = BuildBinaryPlist(metadata);
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(plist);
        }

        return Encoding.ASCII.GetBytes(wrapperPrefix).Concat(compressed.ToArray()).ToArray();
    }

    private static byte[] BuildBinaryPlist(Dictionary<string, object> metadata)
    {
        var objects = new List<byte[]>();
        var keyRefs = new List<byte>();
        var valueRefs = new List<byte>();

        foreach (var (key, value) in metadata)
        {
            keyRefs.Add((byte)objects.Count);
            objects.Add(EncodeString(key));
            valueRefs.Add((byte)objects.Count);
            objects.Add(value switch
            {
                int intValue => EncodeInteger(intValue),
                RawPlistInteger rawInteger => EncodeInteger(rawInteger.Value),
                _ => EncodeString(Convert.ToString(value) ?? string.Empty),
            });
        }

        var dict = new List<byte> { (byte)(0xd0 | metadata.Count) };
        dict.AddRange(keyRefs);
        dict.AddRange(valueRefs);
        var topObject = objects.Count;
        objects.Add(dict.ToArray());

        using var stream = new MemoryStream();
        stream.Write("bplist00"u8);
        var offsets = new List<byte>();
        foreach (var obj in objects)
        {
            offsets.Add((byte)stream.Position);
            stream.Write(obj);
        }

        var offsetTableOffset = stream.Position;
        stream.Write(offsets.ToArray());
        Span<byte> trailer = stackalloc byte[32];
        trailer[6] = 1;
        trailer[7] = 1;
        BinaryPrimitives.WriteUInt64BigEndian(trailer[8..16], (ulong)objects.Count);
        BinaryPrimitives.WriteUInt64BigEndian(trailer[16..24], (ulong)topObject);
        BinaryPrimitives.WriteUInt64BigEndian(trailer[24..32], (ulong)offsetTableOffset);
        stream.Write(trailer);
        return stream.ToArray();
    }

    private static byte[] EncodeString(string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length < 15)
        {
            return [(byte)(0x50 | bytes.Length), .. bytes];
        }

        return [0x5f, 0x10, (byte)bytes.Length, .. bytes];
    }

    private static byte[] EncodeInteger(int value) => [0x10, (byte)value];

    private static byte[] EncodeInteger(ulong value)
    {
        var bytes = new byte[9];
        bytes[0] = 0x13;
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(1), value);
        return bytes;
    }
}

internal sealed record RawPlistInteger(ulong Value);
