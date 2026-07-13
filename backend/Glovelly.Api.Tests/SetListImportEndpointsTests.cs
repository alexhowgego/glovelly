using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Glovelly.Matching;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class SetListImportEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;

    public SetListImportEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Preview_ReadsPrimaryGoogleSheetAndReturnsParsedRows()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);

        var response = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/preview", new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("spreadsheet-123", payload.GetProperty("spreadsheetId").GetString());
        Assert.Equal("Set list", payload.GetProperty("worksheetName").GetString());
        var items = payload.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, item => item.GetProperty("kind").GetString() == "Separator" && !item.GetProperty("include").GetBoolean());
        Assert.Contains(items, item => item.GetProperty("title").GetString() == "L-O-V-E" && item.GetProperty("include").GetBoolean());
    }

    [Fact]
    public async Task Source_WhenResourceIdProvided_ReadsSelectedResourceMetadata()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, _) = await SeedGigWithSetListAsync(factory);
        var selectedResourceId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GigExternalResources.Add(new GigExternalResource
            {
                Id = selectedResourceId,
                GigId = gigId,
                ResourceType = GigExternalResourceType.GoogleSheet,
                Purpose = GigExternalResourcePurpose.SetList,
                Title = "Alternate set list",
                Url = "https://docs.google.com/spreadsheets/d/spreadsheet-456/edit?gid=0#gid=0",
                IsPrimary = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await client.GetAsync($"/gigs/{gigId}/setlist-imports/source?resourceId={selectedResourceId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(selectedResourceId, payload.GetProperty("resourceId").GetGuid());
        Assert.Equal("spreadsheet-456", payload.GetProperty("spreadsheetId").GetString());
        Assert.Equal("spreadsheet-456", sheetsClient.LastMetadataSpreadsheetId);
    }

    [Fact]
    public async Task Source_WhenSheetsConnectionMissing_ReturnsConflict()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory, addConnection: false);

        var response = await client.GetAsync($"/gigs/{gigId}/setlist-imports/source?resourceId={resourceId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Google Sheets is not connected", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Source_WhenMetadataReadFails_ReturnsBadGateway()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient { ThrowOnMetadataRead = true };
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);

        var response = await client.GetAsync($"/gigs/{gigId}/setlist-imports/source?resourceId={resourceId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Google Sheet could not be read", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Source_WhenMetadataHasNoWorksheets_ReturnsUnprocessableEntity()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient { ReturnEmptyMetadata = true };
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);

        var response = await client.GetAsync($"/gigs/{gigId}/setlist-imports/source?resourceId={resourceId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Google Sheet has no worksheets", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Preview_WhenValuesReadFails_ReturnsBadGateway()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient { ThrowOnValuesRead = true };
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);

        var response = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/preview", new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Google Sheet worksheet could not be read", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SaveImport_ReimportKeepsHistoricalImportUntilConfirmed()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);

        var importRequest = new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
            replaceActiveImport = false,
            items = new[]
            {
                new
                {
                    sourceRowNumber = 2,
                    sortOrder = 0,
                    kind = "Separator",
                    include = false,
                    section = (string?)"Set One",
                    padNumber = (string?)null,
                    key = (string?)null,
                    title = "Set One",
                    notes = (string?)null,
                    rawCellsJson = "[\"Set One\"]",
                    confidence = "High",
                },
                new
                {
                    sourceRowNumber = 3,
                    sortOrder = 1,
                    kind = "Song",
                    include = true,
                    section = (string?)"Set One",
                    padNumber = (string?)"74-G",
                    key = (string?)"G",
                    title = "L-O-V-E",
                    notes = (string?)"Callum",
                    rawCellsJson = "[\"74-G\",\"G\",\"L-O-V-E\"]",
                    confidence = "High",
                },
            },
        };

        var firstResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports", importRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var conflictResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports", importRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        var replacementResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports", importRequest with { replaceActiveImport = true }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, replacementResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var imports = await dbContext.GigSetListImports
            .Include(value => value.Items)
            .Where(value => value.GigId == gigId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, imports.Count);
        Assert.Single(imports, value => value.IsActive);
        Assert.All(imports, value => Assert.Equal(2, value.Items.Count));
    }

    [Fact]
    public async Task UpdateImport_EditsSavedSetListItems()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);

        var createResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports", new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
            replaceActiveImport = false,
            items = new[]
            {
                new
                {
                    sourceRowNumber = 3,
                    sortOrder = 0,
                    kind = "Song",
                    include = true,
                    section = (string?)"Set One",
                    padNumber = (string?)"74-G",
                    key = (string?)"G",
                    title = "L-O-V-E",
                    notes = (string?)"Callum",
                    rawCellsJson = "[\"74-G\",\"G\",\"L-O-V-E\"]",
                    confidence = "High",
                },
            },
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var importId = created.GetProperty("id").GetGuid();
        var itemId = created.GetProperty("items")[0].GetProperty("id").GetGuid();

        var updateResponse = await client.PutAsJsonAsync($"/gigs/{gigId}/setlist-imports/{importId}", new
        {
            items = new[]
            {
                new
                {
                    id = itemId,
                    sourceRowNumber = 3,
                    sortOrder = 0,
                    kind = "Song",
                    include = false,
                    section = (string?)"Set Two",
                    padNumber = (string?)"74-G",
                    key = (string?)"F",
                    title = "L-O-V-E edited",
                    notes = (string?)"Audited",
                    rawCellsJson = "[\"74-G\",\"G\",\"L-O-V-E\"]",
                    confidence = "Medium",
                },
            },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var updatedItem = updated.GetProperty("items")[0];
        Assert.Equal("L-O-V-E edited", updatedItem.GetProperty("title").GetString());
        Assert.Equal("F", updatedItem.GetProperty("key").GetString());
        Assert.False(updatedItem.GetProperty("include").GetBoolean());
        Assert.Equal("Medium", updatedItem.GetProperty("confidence").GetString());
    }

    [Fact]
    public async Task Preview_ParsesRowsWithoutForScoreChartMatches()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);
        await SeedForScoreSnapshotAsync(factory, TestAuthContext.UserId, ("LOVE.pdf", "L-O-V-E"));

        var response = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/preview", new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
        }, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var love = payload.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("title").GetString() == "L-O-V-E");
        Assert.True(love.GetProperty("forScoreMatch").ValueKind is JsonValueKind.Null);
        Assert.True(love.GetProperty("forScoreChartId").ValueKind is JsonValueKind.Null);
    }

    [Fact]
    public async Task DraftChartMatchesPreview_ReturnsForScoreChartMatches()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);
        await SeedForScoreSnapshotAsync(factory, TestAuthContext.UserId, ("LOVE.pdf", "L-O-V-E"));

        var previewResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/preview", new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
        }, TestContext.Current.CancellationToken);
        previewResponse.EnsureSuccessStatusCode();
        var previewPayload = await previewResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);

        var matchResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/chart-matches/preview", new
        {
            items = previewPayload.GetProperty("items"),
        }, TestContext.Current.CancellationToken);

        matchResponse.EnsureSuccessStatusCode();
        var payload = await matchResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var love = payload.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("sourceRowNumber").GetInt32() == 3);
        Assert.Equal("Suggested", love.GetProperty("status").GetString());
        Assert.Equal("L-O-V-E", love.GetProperty("selectedChart").GetProperty("title").GetString());
    }

    [Fact]
    public async Task DraftChartMatchAiJobs_StartsJobAndReturnsAcceptedQuickly()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, _) = await SeedGigWithSetListAsync(factory);
        await SeedForScoreSnapshotAsync(factory, TestAuthContext.UserId, ("LOVE.pdf", "L-O-V-E"));

        var response = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/chart-matches/ai-jobs", new
        {
            items = new[]
            {
                new { sourceRowNumber = 3, kind = "Song", include = true, title = "L-O-V-E", padNumber = "74-G", key = "G" },
            },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.NotEqual(Guid.Empty, payload.GetProperty("jobId").GetGuid());
        Assert.Equal("Pending", payload.GetProperty("status").GetString());
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task DraftChartMatchAiJobs_StatusReturnsCompletedResult()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, _) = await SeedGigWithSetListAsync(factory);
        await SeedForScoreSnapshotAsync(factory, TestAuthContext.UserId, ("LOVE.pdf", "L-O-V-E"));

        var startResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/chart-matches/ai-jobs", new
        {
            items = new[]
            {
                new { sourceRowNumber = 3, kind = "Song", include = true, title = "L-O-V-E", padNumber = "74-G", key = "G" },
            },
        }, TestContext.Current.CancellationToken);
        startResponse.EnsureSuccessStatusCode();
        var started = await startResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var jobId = started.GetProperty("jobId").GetGuid();

        var payload = await WaitForJobStatusAsync(client, gigId, jobId, "Completed");

        Assert.Equal("Completed", payload.GetProperty("status").GetString());
        var result = Assert.Single(payload.GetProperty("result").EnumerateArray());
        Assert.Equal(3, result.GetProperty("sourceRowNumber").GetInt32());
        Assert.Equal("Suggested", result.GetProperty("status").GetString());
        Assert.Equal("L-O-V-E", result.GetProperty("selectedChart").GetProperty("title").GetString());
    }

    [Fact]
    public async Task DraftChartMatchAiJobs_StatusIsScopedToOwningUser()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, _) = await SeedGigWithSetListAsync(factory);
        await SeedForScoreSnapshotAsync(factory, TestAuthContext.UserId, ("LOVE.pdf", "L-O-V-E"));

        var startResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/chart-matches/ai-jobs", new
        {
            items = new[]
            {
                new { sourceRowNumber = 3, kind = "Song", include = true, title = "L-O-V-E", padNumber = "74-G", key = "G" },
            },
        }, TestContext.Current.CancellationToken);
        startResponse.EnsureSuccessStatusCode();
        var started = await startResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());
        var otherResponse = await client.GetAsync($"/gigs/{gigId}/setlist-imports/chart-matches/ai-jobs/{started.GetProperty("jobId").GetGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    [Fact]
    public async Task DraftChartMatchAiJobs_ReturnsNotFoundForMissingGigOrJob()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, _) = await SeedGigWithSetListAsync(factory);

        var missingGigResponse = await client.PostAsJsonAsync($"/gigs/{Guid.NewGuid()}/setlist-imports/chart-matches/ai-jobs", new
        {
            items = new[]
            {
                new { sourceRowNumber = 3, kind = "Song", include = true, title = "L-O-V-E", padNumber = "74-G", key = "G" },
            },
        }, TestContext.Current.CancellationToken);
        var missingJobResponse = await client.GetAsync($"/gigs/{gigId}/setlist-imports/chart-matches/ai-jobs/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, missingGigResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingJobResponse.StatusCode);
    }

    [Fact]
    public async Task DraftChartMatchAiJobs_FailedStatusReturnsSafeDiagnostics()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient, services =>
        {
            services.RemoveAll<ISetListChartMatcher>();
            services.AddScoped<ISetListChartMatcher, ThrowingSetListChartMatcher>();
        });
        var client = factory.CreateClient();
        var (gigId, _) = await SeedGigWithSetListAsync(factory);

        var startRequest = new HttpRequestMessage(HttpMethod.Post, $"/gigs/{gigId}/setlist-imports/chart-matches/ai-jobs")
        {
            Content = JsonContent.Create(new
            {
                items = new[]
                {
                    new { sourceRowNumber = 3, kind = "Song", include = true, title = "Sensitive title", padNumber = "74-G", key = "G" },
                },
            }),
        };
        startRequest.Headers.Add("X-Glovelly-Request-Id", "test-correlation-id");
        var startResponse = await client.SendAsync(startRequest, TestContext.Current.CancellationToken);
        startResponse.EnsureSuccessStatusCode();
        var started = await startResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var jobId = started.GetProperty("jobId").GetGuid();

        var payload = await WaitForJobStatusAsync(client, gigId, jobId, "Failed");

        Assert.Equal("Failed", payload.GetProperty("status").GetString());
        Assert.Equal("test-correlation-id", payload.GetProperty("correlationId").GetString());
        Assert.Contains("Chart matching failed", payload.GetProperty("errorMessage").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive title", payload.GetProperty("errorMessage").GetString(), StringComparison.Ordinal);
        Assert.True(payload.GetProperty("result").ValueKind is JsonValueKind.Null);
    }

    [Fact]
    public async Task SaveImport_PreservesForScoreMatchCandidatesForReview()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);
        await SeedForScoreSnapshotAsync(factory, TestAuthContext.UserId, ("LOVE.pdf", "L-O-V-E"));

        var previewResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/preview", new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
        }, TestContext.Current.CancellationToken);
        previewResponse.EnsureSuccessStatusCode();
        var previewPayload = await previewResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);

        var matchResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/chart-matches/preview", new
        {
            items = previewPayload.GetProperty("items"),
        }, TestContext.Current.CancellationToken);
        matchResponse.EnsureSuccessStatusCode();
        var matchPayload = await matchResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var matchesByRow = matchPayload.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("sourceRowNumber").GetInt32());

        var items = previewPayload.GetProperty("items").EnumerateArray().Select(item =>
        {
            var row = item.GetProperty("sourceRowNumber").GetInt32();
            var match = matchesByRow.GetValueOrDefault(row);
            return new
            {
                sourceRowNumber = row,
                sortOrder = item.GetProperty("sortOrder").GetInt32(),
                kind = item.GetProperty("kind").GetString(),
                include = item.GetProperty("include").GetBoolean(),
                section = JsonStringOrNull(item.GetProperty("section")),
                padNumber = JsonStringOrNull(item.GetProperty("padNumber")),
                key = JsonStringOrNull(item.GetProperty("key")),
                title = item.GetProperty("title").GetString(),
                notes = JsonStringOrNull(item.GetProperty("notes")),
                rawCellsJson = item.GetProperty("rawCellsJson").GetString(),
                confidence = item.GetProperty("confidence").GetString(),
                forScoreChartId = match.ValueKind == JsonValueKind.Object && match.GetProperty("selectedChart").ValueKind == JsonValueKind.Object
                    ? match.GetProperty("selectedChart").GetProperty("id").GetString()
                    : null,
                forScoreMatch = match.ValueKind == JsonValueKind.Object ? match : (JsonElement?)null,
            };
        }).ToList();

        var saveResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports", new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
            replaceActiveImport = false,
            items,
        }, TestContext.Current.CancellationToken);
        saveResponse.EnsureSuccessStatusCode();

        var activeResponse = await client.GetAsync($"/gigs/{gigId}/setlist-imports/active", TestContext.Current.CancellationToken);
        activeResponse.EnsureSuccessStatusCode();
        var activePayload = await activeResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var love = activePayload.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("title").GetString() == "L-O-V-E");
        Assert.Equal("Suggested", love.GetProperty("forScoreMatch").GetProperty("status").GetString());
        Assert.NotEmpty(love.GetProperty("forScoreMatch").GetProperty("candidates").EnumerateArray());
    }

    [Fact]
    public async Task SaveImport_RejectsChartFromAnotherUser()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);
        var otherChartId = await SeedForScoreSnapshotAsync(factory, TestAuthContext.AlternateUserId, ("Other.pdf", "Other"));

        var response = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports", new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
            replaceActiveImport = false,
            items = new[]
            {
                new
                {
                    sourceRowNumber = 3,
                    sortOrder = 0,
                    kind = "Song",
                    include = true,
                    section = (string?)"Set One",
                    padNumber = (string?)"74-G",
                    key = (string?)"G",
                    title = "L-O-V-E",
                    notes = (string?)null,
                    rawCellsJson = "[\"74-G\",\"G\",\"L-O-V-E\"]",
                    confidence = "High",
                    forScoreChartId = otherChartId,
                },
            },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExistingSetList_CanPreviewAndSaveChartMappingWithoutReplacingImport()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, resourceId) = await SeedGigWithSetListAsync(factory);
        var chartId = await SeedForScoreSnapshotAsync(factory, TestAuthContext.UserId, ("LOVE.pdf", "L-O-V-E"));

        var createResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports", new
        {
            resourceId,
            worksheetId = "0",
            worksheetName = "Set list",
            replaceActiveImport = false,
            items = new[]
            {
                new
                {
                    sourceRowNumber = 3,
                    sortOrder = 0,
                    kind = "Song",
                    include = true,
                    section = (string?)"Set One",
                    padNumber = (string?)"74-G",
                    key = (string?)"G",
                    title = "L-O-V-E",
                    notes = (string?)null,
                    rawCellsJson = "[\"74-G\",\"G\",\"L-O-V-E\"]",
                    confidence = "High",
                },
            },
        }, TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var importId = created.GetProperty("id").GetGuid();
        var itemId = created.GetProperty("items")[0].GetProperty("id").GetGuid();

        var previewResponse = await client.PostAsJsonAsync($"/gigs/{gigId}/setlist-imports/{importId}/chart-matches/preview", new { }, TestContext.Current.CancellationToken);
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Suggested", preview.GetProperty("items")[0].GetProperty("status").GetString());

        var updateResponse = await client.PutAsJsonAsync($"/gigs/{gigId}/setlist-imports/{importId}", new
        {
            items = new[]
            {
                new
                {
                    id = itemId,
                    sourceRowNumber = 3,
                    sortOrder = 0,
                    kind = "Song",
                    include = true,
                    section = (string?)"Set One",
                    padNumber = (string?)"74-G",
                    key = (string?)"G",
                    title = "L-O-V-E",
                    notes = (string?)null,
                    rawCellsJson = "[\"74-G\",\"G\",\"L-O-V-E\"]",
                    confidence = "High",
                    forScoreChartId = chartId,
                },
            },
        }, TestContext.Current.CancellationToken);

        updateResponse.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.GigSetListItems.SingleAsync(value => value.Id == itemId, TestContext.Current.CancellationToken);
        Assert.Equal(chartId, item.ForScoreChartId);
        Assert.Equal(ForScoreMappingStatus.Linked, item.ForScoreMappingStatus);
    }

    [Fact]
    public async Task ForScoreExport_Returns4ssWithIncludedSongsInSavedOrder()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, _) = await SeedGigWithSetListAsync(factory);
        await SeedActiveSetListImportAsync(factory, gigId,
            new TestSetListItem(GigSetListItemKind.Song, true, 2, "Second", Guid.NewGuid(), "Second Chart", "Second.Pdf"),
            new TestSetListItem(GigSetListItemKind.Separator, false, 0, "Set One", null, null, null),
            new TestSetListItem(GigSetListItemKind.Song, true, 1, "First", Guid.NewGuid(), "First Chart", "First.pdf"),
            new TestSetListItem(GigSetListItemKind.Song, false, 3, "Excluded", Guid.NewGuid(), "Excluded Chart", "Excluded.pdf"),
            new TestSetListItem(GigSetListItemKind.Comment, false, 4, "Note", null, null, null));

        var response = await client.GetAsync($"/gigs/{gigId}/setlist-imports/active/forscore-export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Setlist test gig.4ss", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = Assert.IsType<XElement>(document.Root);
        Assert.Equal("forScore", root.Name.LocalName);
        Assert.Equal("setlist", root.Attribute("kind")?.Value);
        Assert.Equal("1.0", root.Attribute("version")?.Value);
        Assert.Equal("Setlist test gig", root.Attribute("title")?.Value);
        var scores = root.Elements("score").ToList();
        Assert.Equal(2, scores.Count);
        Assert.Equal("First Chart", scores[0].Attribute("title")?.Value);
        Assert.Equal("First.pdf", scores[0].Attribute("path")?.Value);
        Assert.Equal("Second Chart", scores[1].Attribute("title")?.Value);
        Assert.Equal("Second.Pdf", scores[1].Attribute("path")?.Value);
    }

    [Fact]
    public async Task ForScoreExport_EscapesXmlSensitiveValues()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, _) = await SeedGigWithSetListAsync(factory, title: "Bella & Roxie's <Show>");
        await SeedActiveSetListImportAsync(factory, gigId,
            new TestSetListItem(
                GigSetListItemKind.Song,
                true,
                0,
                "Fallback",
                Guid.NewGuid(),
                "B-017 Jump, Jive An' Wail \"Piano\"",
                "Charts/B-017 Jump, Jive An' Wail & Piano.pdf"));

        var response = await client.GetAsync($"/gigs/{gigId}/setlist-imports/active/forscore-export", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.Root!;
        var score = Assert.Single(root.Elements("score"));
        Assert.Equal("Bella & Roxie's <Show>", root.Attribute("title")?.Value);
        Assert.Equal("B-017 Jump, Jive An' Wail \"Piano\"", score.Attribute("title")?.Value);
        Assert.Equal("Charts/B-017 Jump, Jive An' Wail & Piano.pdf", score.Attribute("path")?.Value);
    }

    [Fact]
    public async Task ForScoreExport_RejectsInaccessibleGigAndMissingActiveSetList()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var factory = CreateFactory(sheetsClient);
        var client = factory.CreateClient();
        var (gigId, _) = await SeedGigWithSetListAsync(factory);

        client.DefaultRequestHeaders.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());
        var inaccessibleResponse = await client.GetAsync($"/gigs/{gigId}/setlist-imports/active/forscore-export", TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        var missingActiveResponse = await client.GetAsync($"/gigs/{gigId}/setlist-imports/active/forscore-export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, inaccessibleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingActiveResponse.StatusCode);
        var missingPayload = await missingActiveResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("No active set list is available for this gig.", missingPayload.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ForScoreExport_RejectsEmptyAndUnmappedIncludedSongs()
    {
        var sheetsClient = new FakeGoogleSheetsApiClient();
        using var emptyFactory = CreateFactory(sheetsClient);
        var emptyClient = emptyFactory.CreateClient();
        var (emptyGigId, _) = await SeedGigWithSetListAsync(emptyFactory);
        await SeedActiveSetListImportAsync(emptyFactory, emptyGigId,
            new TestSetListItem(GigSetListItemKind.Separator, false, 0, "Set One", null, null, null));

        var emptyResponse = await emptyClient.GetAsync($"/gigs/{emptyGigId}/setlist-imports/active/forscore-export", TestContext.Current.CancellationToken);

        using var unmappedFactory = CreateFactory(new FakeGoogleSheetsApiClient());
        var unmappedClient = unmappedFactory.CreateClient();
        var (unmappedGigId, _) = await SeedGigWithSetListAsync(unmappedFactory);
        await SeedActiveSetListImportAsync(unmappedFactory, unmappedGigId,
            new TestSetListItem(GigSetListItemKind.Song, true, 0, "Needs Chart", null, null, null));

        var unmappedResponse = await unmappedClient.GetAsync($"/gigs/{unmappedGigId}/setlist-imports/active/forscore-export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unmappedResponse.StatusCode);
        var payload = await unmappedResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Select forScore charts for all included song rows before exporting.", payload.GetProperty("message").GetString());
        var missing = Assert.Single(payload.GetProperty("missingItems").EnumerateArray());
        Assert.Equal("Needs Chart", missing.GetProperty("title").GetString());
    }

    private WebApplicationFactory<Program> CreateFactory(FakeGoogleSheetsApiClient sheetsClient, Action<IServiceCollection>? configureServices = null)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleSheetsApiClient>();
                services.AddSingleton<IGoogleSheetsApiClient>(sheetsClient);
                configureServices?.Invoke(services);
            });
        });
    }

    private static async Task<JsonElement> WaitForJobStatusAsync(HttpClient client, Guid gigId, Guid jobId, string expectedStatus)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var response = await client.GetAsync($"/gigs/{gigId}/setlist-imports/chart-matches/ai-jobs/{jobId}", TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
            if (string.Equals(payload.GetProperty("status").GetString(), expectedStatus, StringComparison.Ordinal))
            {
                return payload;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for chart match job {jobId} to reach {expectedStatus}.");
    }

    private static async Task<(Guid GigId, Guid ResourceId)> SeedGigWithSetListAsync(WebApplicationFactory<Program> factory, bool addConnection = true, string title = "Setlist test gig")
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenProtector = scope.ServiceProvider.GetRequiredService<IGoogleTokenProtector>();
        var gigId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        if (addConnection)
        {
            dbContext.GoogleConnections.Add(new GoogleConnection
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                EncryptedAccessToken = tokenProtector.Protect("ya29.sheets"),
                EncryptedRefreshToken = tokenProtector.Protect("1//sheets"),
                AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                GrantedScopes = GoogleScopes.SpreadsheetsReadonly,
                TokenType = "Bearer",
                ConnectedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        dbContext.Gigs.Add(new Gig
        {
            Id = gigId,
            ClientId = TestData.FoxAndFinchId,
            CreatedByUserId = TestAuthContext.UserId,
            UpdatedByUserId = TestAuthContext.UserId,
            Title = title,
            Date = new DateOnly(2026, 6, 5),
            Venue = "Test venue",
            Fee = 1000,
            Status = GigStatus.Confirmed,
            ExternalResources =
            [
                new GigExternalResource
                {
                    Id = resourceId,
                    ResourceType = GigExternalResourceType.GoogleSheet,
                    Purpose = GigExternalResourcePurpose.SetList,
                    Title = "Primary set list",
                    Url = "https://docs.google.com/spreadsheets/d/spreadsheet-123/edit?gid=0#gid=0",
                    IsPrimary = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
            ],
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (gigId, resourceId);
    }

    private static async Task SeedActiveSetListImportAsync(WebApplicationFactory<Program> factory, Guid gigId, params TestSetListItem[] items)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.GigSetListImports.Add(new GigSetListImport
        {
            Id = Guid.NewGuid(),
            GigId = gigId,
            SpreadsheetId = "spreadsheet-123",
            WorksheetName = "Set list",
            IsActive = true,
            ImportedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Items = items.Select((item, index) => new GigSetListItem
            {
                Id = Guid.NewGuid(),
                SourceRowNumber = index + 1,
                SortOrder = item.SortOrder,
                Kind = item.Kind,
                Include = item.Include,
                Title = item.Title,
                RawCellsJson = "[]",
                Confidence = GigSetListItemConfidence.High,
                ForScoreChartId = item.ChartId,
                ForScoreLibrarySnapshotId = item.ChartId.HasValue ? Guid.NewGuid() : null,
                ForScoreChartTitle = item.ChartTitle,
                ForScoreChartFilePath = item.ChartFilePath,
                ForScoreMappingStatus = item.ChartId.HasValue ? ForScoreMappingStatus.Linked : ForScoreMappingStatus.Unmapped,
                ForScoreMappingConfidence = item.ChartId.HasValue ? ForScoreMappingConfidence.Manual : ForScoreMappingConfidence.None,
                ForScoreMappingUpdatedAtUtc = item.ChartId.HasValue ? DateTimeOffset.UtcNow : null,
            }).ToList(),
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> SeedForScoreSnapshotAsync(WebApplicationFactory<Program> factory, Guid userId, params (string FilePath, string Title)[] charts)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (userId == TestAuthContext.AlternateUserId && !await dbContext.Users.AnyAsync(user => user.Id == userId, TestContext.Current.CancellationToken))
        {
            dbContext.Users.Add(new User
            {
                Id = userId,
                GoogleSubject = "alternate-subject",
                Email = "alternate@glovelly.local",
                DisplayName = "Alternate User",
                Role = UserRole.User,
                IsActive = true,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        var snapshot = new ForScoreLibrarySnapshot
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = userId,
            OriginalFileName = "library.4sb",
            SourceFormat = "FourSb",
            IsActive = true,
            ChartCount = charts.Length,
            WarningsJson = "[]",
            ImportedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Charts = charts.Select((chart, index) => new ForScoreChart
            {
                Id = Guid.NewGuid(),
                SortOrder = index,
                FilePath = chart.FilePath,
                Title = chart.Title,
                NormalizedTitle = MatchTextNormalizer.Normalize(chart.Title).Canonical,
            }).ToList(),
        };
        dbContext.ForScoreLibrarySnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return snapshot.Charts.First().Id;
    }

    private static string? JsonStringOrNull(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Null ? null : element.GetString();
    }

    private sealed class FakeGoogleSheetsApiClient : IGoogleSheetsApiClient
    {
        public bool ThrowOnMetadataRead { get; set; }
        public bool ThrowOnValuesRead { get; set; }
        public bool ReturnEmptyMetadata { get; set; }
        public string? LastMetadataSpreadsheetId { get; private set; }

        public Task<GoogleSpreadsheetMetadata> GetSpreadsheetMetadataAsync(
            GoogleConnectionAccessToken accessToken,
            string spreadsheetId,
            CancellationToken cancellationToken)
        {
            LastMetadataSpreadsheetId = spreadsheetId;
            if (ThrowOnMetadataRead)
            {
                throw new InvalidOperationException("Google returned 403.");
            }

            return Task.FromResult(new GoogleSpreadsheetMetadata(
                spreadsheetId,
                ReturnEmptyMetadata ? [] : [new GoogleSheetMetadata("0", "Set list", 0)]));
        }

        public Task<GoogleSheetValues> GetWorksheetValuesAsync(
            GoogleConnectionAccessToken accessToken,
            string spreadsheetId,
            string worksheetName,
            CancellationToken cancellationToken)
        {
            if (ThrowOnValuesRead)
            {
                throw new InvalidOperationException("Google returned 404.");
            }

            IReadOnlyList<IReadOnlyList<string>> rows =
            [
                ["Pad #", "Key", "Song", "", "Vocalist"],
                ["Set One"],
                ["74-G", "G", "L-O-V-E", "", "Callum"],
                ["", "", "Please delete old parts"],
            ];
            return Task.FromResult(new GoogleSheetValues("Set list", rows));
        }
    }

    private sealed class ThrowingSetListChartMatcher : ISetListChartMatcher
    {
        public Task<IReadOnlyList<SetListChartMatchResult>> MatchAsync(
            Guid? userId,
            IReadOnlyList<SetListChartMatchInput> items,
            CancellationToken cancellationToken = default,
            bool useConfiguredRanker = true)
        {
            throw new InvalidOperationException("Sensitive provider response should not be exposed.");
        }
    }

    private sealed record TestSetListItem(
        GigSetListItemKind Kind,
        bool Include,
        int SortOrder,
        string Title,
        Guid? ChartId,
        string? ChartTitle,
        string? ChartFilePath);
}
