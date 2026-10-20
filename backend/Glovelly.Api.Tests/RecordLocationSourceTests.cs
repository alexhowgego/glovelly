using System.Text.Json;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class RecordLocationSourceTests
{
    [Fact]
    public void QueryAndFingerprintMatchSharedBrowserVectors()
    {
        var vectors = JsonSerializer.Deserialize<List<SourceVector>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "record-location-sources.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        foreach (var vector in vectors)
        {
            var query = vector.Address is null
                ? RecordLocationSource.GigQuery(vector.Venue)
                : RecordLocationSource.ClientQuery(vector.Address);
            Assert.Equal(vector.Query, query);
            Assert.Equal(vector.Fingerprint, RecordLocationSource.Fingerprint(query));
        }

        Assert.Equal(string.Empty, RecordLocationSource.ClientQuery(null));
        Assert.Equal(string.Empty, RecordLocationSource.GigQuery(null));
    }

    private sealed record SourceVector(string Name, Address? Address, string? Venue, string Query, string Fingerprint);
}
