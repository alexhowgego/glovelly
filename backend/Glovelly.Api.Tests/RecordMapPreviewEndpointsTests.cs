using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class RecordMapPreviewEndpointsTests(GlovellyApiFactory factory) : IClassFixture<GlovellyApiFactory>
{
    [Fact]
    public async Task VisibleSavedAddressAutomaticallyReceivesNoStoreDescriptorWithoutMutation()
    {
        using var seed = factory.CreateClient();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<GoogleStaticMapsSettings>(settings =>
            { settings.Enabled = true; settings.ApiKey = "test-only"; settings.SigningSecret = GoogleStaticMapsSigningTests.TestSecret; })));
        using var http = configured.CreateClient();
        var path = $"/clients/{TestData.FoxAndFinchId}";
        var client = (await http.GetFromJsonAsync<JsonObject>(path, TestContext.Current.CancellationToken))!;
        Assert.Null(client["mapPreviewEnabled"]);
        Assert.Null(client["mapPreviewApproval"]);
        var response = await http.GetAsync($"{path}/map-preview?size=small&source={client["mapPreviewSourceFingerprint"]}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.True(response.Headers.CacheControl.Private);
        var descriptor = await response.Content.ReadFromJsonAsync<StaticMapDescriptor>(TestContext.Current.CancellationToken);
        Assert.NotNull(descriptor);
        Assert.StartsWith("https://maps.googleapis.com/maps/api/staticmap?", descriptor.Url);
        Assert.Equal(client["mapPreviewSourceFingerprint"]!.GetValue<string>(), descriptor.SourceFingerprint);
        Assert.Equal(client.ToJsonString(), (await http.GetFromJsonAsync<JsonObject>(path, TestContext.Current.CancellationToken))!.ToJsonString());
        var fitted = await http.GetFromJsonAsync<StaticMapDescriptor>($"{path}/map-preview?size=580x400&source={descriptor.SourceFingerprint}", TestContext.Current.CancellationToken);
        Assert.Equal("580x400", fitted!.Size);
        Assert.Contains("size=580x400", fitted.Url);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync($"{path}/map-preview?size=641x400&source={descriptor.SourceFingerprint}", TestContext.Current.CancellationToken)).StatusCode);

        client["billingAddress"]!["line1"] = "New saved hall";
        (await http.PutAsJsonAsync(path, client, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await http.GetAsync($"{path}/map-preview?size=small&source={descriptor.SourceFingerprint}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task SharedClientAndSavedGigUseTheirOwnSources()
    {
        using var seed = factory.CreateClient();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<GoogleStaticMapsSettings>(settings =>
            { settings.Enabled = true; settings.ApiKey = "test-only"; settings.SigningSecret = GoogleStaticMapsSigningTests.TestSecret; })));
        using var http = configured.CreateClient();
        using var scope = configured.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sharedId = Guid.NewGuid();
        db.Clients.Add(new Client { Id = sharedId, Name = "Shared", BillingAddress = new Address { Line1 = "Shared Hall" } });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var source = RecordLocationSource.Fingerprint("Shared Hall");
        var shared = await http.GetAsync($"/clients/{sharedId}/map-preview?size=small&source={source}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, shared.StatusCode);
        var created = await http.PostAsJsonAsync("/gigs", new
        { clientId = TestData.FoxAndFinchId, title = "Concert", date = "2030-01-01", venue = "Different Venue", fee = 100, status = "Confirmed" }, TestContext.Current.CancellationToken);
        created.EnsureSuccessStatusCode();
        var gig = (await created.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken))!;
        var response = await http.GetAsync($"/gigs/{gig["id"]}/map-preview?size=medium&source={gig["mapPreviewSourceFingerprint"]}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var descriptor = (await response.Content.ReadFromJsonAsync<StaticMapDescriptor>(TestContext.Current.CancellationToken))!;
        Assert.Equal("Different Venue", Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(descriptor.Url).Query)["center"]);
        Assert.Null(gig["mapPreviewApproval"]);
        Assert.Null(gig["mapPreviewEnabled"]);
    }

    [Fact]
    public async Task UnavailableConfigurationMalformedAndInvisibleRequestsDiscloseNoUrl()
    {
        using var http = factory.CreateClient();
        var path = $"/clients/{TestData.FoxAndFinchId}";
        var client = (await http.GetFromJsonAsync<JsonObject>(path, TestContext.Current.CancellationToken))!;
        var source = client["mapPreviewSourceFingerprint"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.NoContent, (await http.GetAsync($"{path}/map-preview?size=small&source={source}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync($"{path}/map-preview?size=small&source={source}&query=arbitrary", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync($"{path}/map-preview?size=arbitrary&source={source}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync($"{path}/map-preview?size=small&source=invalid", TestContext.Current.CancellationToken)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otherId = Guid.NewGuid();
        db.Clients.Add(new Client { Id = otherId, Name = "Other", CreatedByUserId = Guid.NewGuid() });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/clients/{otherId}/map-preview?size=small&source={source}", TestContext.Current.CancellationToken)).StatusCode);
    }
}
