using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GoogleCalendarOAuthTests : IClassFixture<GlovellyApiFactory>
{
    private readonly GlovellyApiFactory factory;
    public GoogleCalendarOAuthTests(GlovellyApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task ConnectAndCallback_UseSharedCallbackAndRunCalendarSetupInOrder()
    {
        var calls = new List<string>();
        var tokens = new FakeTokens { Scope = $"{GoogleScopes.CalendarAppCreated} {GoogleScopes.DocumentsReadonly}" };
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "client");
            builder.UseSetting("Authentication:Google:ClientSecret", "secret");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleOAuthTokenClient>(); services.AddSingleton<IGoogleOAuthTokenClient>(tokens);
                services.RemoveAll<IGoogleCalendarIntegrationService>(); services.AddSingleton<IGoogleCalendarIntegrationService>(new FakeCalendar(calls));
                services.RemoveAll<ICalendarSyncWorkQueue>(); services.AddSingleton<ICalendarSyncWorkQueue>(new FakeQueue(calls));
            });
        });
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.GoogleConnections.Add(new GoogleConnection { Id = Guid.NewGuid(), UserId = TestAuthContext.UserId,
                GrantedScopes = GoogleScopes.DocumentsReadonly, EncryptedAccessToken = "existing", EncryptedRefreshToken = "existing",
                TokenType = "Bearer", ConnectedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var connect = await client.GetAsync("/integrations/google-calendar/connect", TestContext.Current.CancellationToken);
        var query = QueryHelpers.ParseQuery(connect.Headers.Location!.Query);
        Assert.Equal("http://localhost/integrations/google/callback", query["redirect_uri"]);
        Assert.Contains(GoogleScopes.CalendarAppCreated, query["scope"].ToString());
        Assert.Contains(GoogleScopes.DocumentsReadonly, query["scope"].ToString());
        var result = await client.GetAsync($"/integrations/google/callback?code=ok&state={Uri.EscapeDataString(query["state"].ToString())}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal("/?integration=google-calendar&status=callback-received", result.Headers.Location?.OriginalString);
        Assert.Equal("http://localhost/integrations/google/callback", tokens.RedirectUri);
        Assert.Equal(new[] { "ensure", "invalidate", "enqueue" }, calls);
        using var check = app.Services.CreateScope();
        var connection = await check.ServiceProvider.GetRequiredService<AppDbContext>().GoogleConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains(GoogleScopes.DocumentsReadonly, connection.GrantedScopes);
        Assert.Contains(GoogleScopes.CalendarAppCreated, connection.GrantedScopes);
    }

    [Fact]
    public async Task CalendarDisconnect_DisablesSettingsAndPreservesOtherScopes()
    {
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var id = Guid.NewGuid();
            db.GoogleConnections.Add(new GoogleConnection { Id = id, UserId = TestAuthContext.UserId,
                GrantedScopes = $"{GoogleScopes.CalendarAppCreated} {GoogleScopes.DriveFile}", EncryptedAccessToken = "access", EncryptedRefreshToken = "refresh", TokenType = "Bearer" });
            db.GoogleCalendarIntegrationSettings.Add(new GoogleCalendarIntegrationSettings { Id = Guid.NewGuid(), UserId = TestAuthContext.UserId, GoogleConnectionId = id, IsEnabled = true, RequiresReconnection = true });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/integrations/google-calendar/disconnect", null, TestContext.Current.CancellationToken)).StatusCode);
        using var check = factory.Services.CreateScope();
        var dbCheck = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await dbCheck.GoogleCalendarIntegrationSettings.SingleAsync(TestContext.Current.CancellationToken);
        var connection = await dbCheck.GoogleConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(settings.IsEnabled); Assert.False(settings.RequiresReconnection); Assert.NotNull(settings.DisconnectedAtUtc);
        Assert.Equal(GoogleScopes.DriveFile, connection.GrantedScopes); Assert.NotEmpty(connection.EncryptedRefreshToken);
    }

    [Theory]
    [InlineData("google-drive", "Google Drive")]
    [InlineData("google-calendar", "Google Calendar")]
    public async Task Callback_Denied_IdentifiesService(string integration, string name)
    {
        using var app = factory.WithWebHostBuilder(builder => { builder.UseSetting("Authentication:Google:ClientId", "client"); builder.UseSetting("Authentication:Google:ClientSecret", "secret"); });
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var connect = await client.GetAsync($"/integrations/{integration}/connect", TestContext.Current.CancellationToken);
        var state = QueryHelpers.ParseQuery(connect.Headers.Location!.Query)["state"].ToString();
        var result = await client.GetAsync($"/integrations/google/callback?error=access_denied&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);
        var problem = await result.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal($"{name} connection was not approved.", problem.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("google-drive", "Drive")]
    [InlineData("google-calendar", "Calendar")]
    public async Task Callback_MissingScope_DoesNotRunServiceSetup(string integration, string scopeLabel)
    {
        var calls = new List<string>();
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "client");
            builder.UseSetting("Authentication:Google:ClientSecret", "secret");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleOAuthTokenClient>(); services.AddSingleton<IGoogleOAuthTokenClient>(new FakeTokens { Scope = GoogleScopes.DocumentsReadonly });
                services.RemoveAll<IGoogleCalendarIntegrationService>(); services.AddSingleton<IGoogleCalendarIntegrationService>(new FakeCalendar(calls));
                services.RemoveAll<ICalendarSyncWorkQueue>(); services.AddSingleton<ICalendarSyncWorkQueue>(new FakeQueue(calls));
            });
        });
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var connect = await client.GetAsync($"/integrations/{integration}/connect", TestContext.Current.CancellationToken);
        var state = QueryHelpers.ParseQuery(connect.Headers.Location!.Query)["state"].ToString();
        var result = await client.GetAsync($"/integrations/google/callback?code=test&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var problem = await result.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Contains(scopeLabel, problem.GetProperty("errors").GetProperty("scope")[0].GetString());
        Assert.Empty(calls);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.GoogleDriveIntegrationSettings.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.GoogleConnections.ToListAsync(TestContext.Current.CancellationToken));
    }

    private sealed class FakeCalendar(List<string> calls) : IGoogleCalendarIntegrationService
    {
        public Task<GoogleCalendarIntegrationSettings> EnsureCalendarAsync(Guid userId, CancellationToken cancellationToken) { calls.Add("ensure"); return Task.FromResult(new GoogleCalendarIntegrationSettings()); }
        public Task InvalidateSyncHashesAsync(Guid userId, CancellationToken cancellationToken) { calls.Add("invalidate"); return Task.CompletedTask; }
    }
    private sealed class FakeQueue(List<string> calls) : ICalendarSyncWorkQueue
    {
        public Task EnqueueGigAsync(Guid userId, Guid gigId, CalendarSyncWorkItemReason reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnqueueFullSyncAsync(Guid userId, CalendarSyncWorkItemReason reason, CancellationToken cancellationToken = default) { Assert.Equal(CalendarSyncWorkItemReason.ConnectionChanged, reason); calls.Add("enqueue"); return Task.CompletedTask; }
    }
    private sealed class FakeTokens : IGoogleOAuthTokenClient
    {
        public string Scope { get; set; } = "";
        public string? RedirectUri { get; private set; }
        public Task<GoogleOAuthTokenExchangeResult> ExchangeCodeAsync(string code, string redirectUri, string clientId, string clientSecret, CancellationToken cancellationToken)
        {
            RedirectUri = redirectUri;
            return Task.FromResult(new GoogleOAuthTokenExchangeResult(true, 200, "", new GoogleOAuthTokenResponse { AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600, Scope = Scope, TokenType = "Bearer" }));
        }
        public Task<GoogleOAuthTokenRefreshResult> RefreshAccessTokenAsync(string refreshToken, string clientId, string clientSecret, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
