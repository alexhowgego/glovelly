using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
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

    private WebApplicationFactory<Program> CreateFactory(FakeGoogleSheetsApiClient sheetsClient)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleSheetsApiClient>();
                services.AddSingleton<IGoogleSheetsApiClient>(sheetsClient);
            });
        });
    }

    private static async Task<(Guid GigId, Guid ResourceId)> SeedGigWithSetListAsync(WebApplicationFactory<Program> factory, bool addConnection = true)
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
            Title = "Setlist test gig",
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
}
