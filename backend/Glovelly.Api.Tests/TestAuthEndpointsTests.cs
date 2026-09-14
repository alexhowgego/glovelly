using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class TestAuthEndpointsTests
{
    private const string UatSecret = "shared-uat-secret";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Login_IsUnavailableOutsideStaging()
    {
        await using var factory = CreateFactory("Production");
        var client = factory.CreateClient();

        var response = await client.PostAsync("/test-auth/login", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutSecret_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory("Staging");
        var client = factory.CreateClient();

        var response = await client.PostAsync("/test-auth/login", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidSecret_ReturnsForbidden()
    {
        await using var factory = CreateFactory("Staging");
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/test-auth/login");
        request.Headers.Add("X-Glovelly-Uat-Secret", "wrong-secret");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidSecret_SignsInRegressionUserAndSeedsBaselineData()
    {
        await using var factory = CreateFactory("Staging");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/test-auth/login");
        request.Headers.Add("X-Glovelly-Uat-Secret", UatSecret);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Set-Cookie"));

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(UatRegressionDataSeeder.Email, payload.GetProperty("email").GetString());

        var meResponse = await client.GetAsync("/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var mePayload = await meResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(UatRegressionDataSeeder.Email, mePayload.GetProperty("email").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.NotNull(await dbContext.Users.FindAsync([UatRegressionDataSeeder.UserId], TestContext.Current.CancellationToken));
        Assert.NotNull(await dbContext.Clients.FindAsync([UatRegressionDataSeeder.ClientId], TestContext.Current.CancellationToken));
        Assert.NotNull(await dbContext.SellerProfiles.FindAsync([UatRegressionDataSeeder.SellerProfileId], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reset_WithValidSecret_RemovesUatDataAndRestoresBaselineFixture()
    {
        await using var factory = CreateFactory("Staging");
        var client = factory.CreateClient();
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/test-auth/login");
        loginRequest.Headers.Add("X-Glovelly-Uat-Secret", UatSecret);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(loginRequest, TestContext.Current.CancellationToken)).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Clients.Add(new Client
            {
                Id = Guid.NewGuid(),
                Name = "Temporary UAT client",
                CreatedByUserId = UatRegressionDataSeeder.UserId,
                UpdatedByUserId = UatRegressionDataSeeder.UserId,
            });
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var resetRequest = new HttpRequestMessage(HttpMethod.Post, "/test-auth/reset");
        resetRequest.Headers.Add("X-Glovelly-Uat-Secret", UatSecret);
        var resetResponse = await client.SendAsync(resetRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);
        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clients = await verificationDb.Clients
            .Where(value => value.CreatedByUserId == UatRegressionDataSeeder.UserId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(clients);
        Assert.Equal(UatRegressionDataSeeder.ClientId, clients[0].Id);
    }

    [Fact]
    public async Task GigImportBatchSetup_WithValidSecret_CreatesRegressionImportBatch()
    {
        await using var factory = CreateFactory("Staging");
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/test-auth/gig-import-batches")
        {
            Content = JsonContent.Create(new { sourceName = "UAT import setup" }),
        };
        request.Headers.Add("X-Glovelly-Uat-Secret", UatSecret);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var batchId = payload.GetProperty("batchId").GetGuid();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = await dbContext.GigImportBatches.FindAsync([batchId], TestContext.Current.CancellationToken);
        Assert.NotNull(batch);
        Assert.Equal(UatRegressionDataSeeder.UserId, batch.CreatedByUserId);
        Assert.Equal(GigImportBatchStatus.Draft, batch.Status);
        Assert.Equal(3, dbContext.GigImportDrafts.Count(draft => draft.BatchId == batchId));
    }

    private static GlovellyApiFactory CreateFactory(string environmentName)
    {
        return new GlovellyApiFactory()
            .WithConfiguration(new Dictionary<string, string?>
            {
                ["App:PublicBaseUrl"] = "https://staging.glovelly.test",
                ["Uat:Secret"] = UatSecret,
            })
            .WithEnvironment(environmentName);
    }
}
