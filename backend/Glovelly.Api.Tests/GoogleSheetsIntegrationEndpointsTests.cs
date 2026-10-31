using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GoogleSheetsIntegrationEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public GoogleSheetsIntegrationEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Connect_RedirectsToGoogleAuthorizationEndpointWithSheetsScopeOnly()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "google-client-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "google-client-secret");
        });
        var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/integrations/google-sheets/connect", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("google-client-id", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(
            "openid email profile https://www.googleapis.com/auth/spreadsheets.readonly",
            query["scope"]);
        Assert.Equal("http://localhost/integrations/google/callback", query["redirect_uri"]);
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
    }

    [Fact]
    public async Task ConnectDocs_RequestsDocumentScopeWithoutSheetsScope()
    {
        using var factory = CreateConfiguredFactory(new FakeGoogleSheetsOAuthTokenExchanger());
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/integrations/google-docs/connect", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var scopes = QueryHelpers.ParseQuery(response.Headers.Location!.Query)["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(GoogleScopes.DocumentsReadonly, scopes);
        Assert.DoesNotContain(GoogleScopes.SpreadsheetsReadonly, scopes);
    }

    [Fact]
    public async Task CallbackDocs_UsesProtectedStateAndSavesDocumentScope()
    {
        var exchanger = new FakeGoogleSheetsOAuthTokenExchanger { Response = new GoogleOAuthTokenResponse { AccessToken = "test", RefreshToken = "refresh", ExpiresIn = 3600, Scope = GoogleScopes.DocumentsReadonly, TokenType = "Bearer" } };
        using var factory = CreateConfiguredFactory(exchanger);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var connect = await client.GetAsync("/integrations/google-docs/connect", TestContext.Current.CancellationToken);
        var state = QueryHelpers.ParseQuery(connect.Headers.Location!.Query)["state"].ToString();
        var callback = await client.GetAsync($"/integrations/google/callback?code=test&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/?integration=google-docs&status=callback-received", callback.Headers.Location?.OriginalString);
        using var scope = factory.Services.CreateScope();
        var connection = await scope.ServiceProvider.GetRequiredService<AppDbContext>().GoogleConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains(GoogleScopes.DocumentsReadonly, connection.GrantedScopes);
    }

    [Fact]
    public async Task Connect_WhenDriveScopeExists_RequestsDriveAndSheetsScopes()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "google-client-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "google-client-secret");
        });
        var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
        });
        await SeedGoogleConnectionAsync(factory, GoogleScopes.DriveFile);

        var response = await client.GetAsync("/integrations/google-sheets/connect", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        var query = QueryHelpers.ParseQuery(location.Query);
        var scopes = query["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(GoogleScopes.DriveFile, scopes);
        Assert.Contains(GoogleScopes.SpreadsheetsReadonly, scopes);
    }

    [Fact]
    public async Task Connect_WhenCalendarScopeExists_RequestsSheetsAndCalendarScopes()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "google-client-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "google-client-secret");
        });
        var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
        });
        await SeedGoogleConnectionAsync(factory, GoogleScopes.CalendarAppCreated);

        var response = await client.GetAsync("/integrations/google-sheets/connect", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        var query = QueryHelpers.ParseQuery(location.Query);
        var scopes = query["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(GoogleScopes.SpreadsheetsReadonly, scopes);
        Assert.Contains(GoogleScopes.CalendarAppCreated, scopes);
    }

    [Fact]
    public async Task Callback_WithCodeAndState_SavesSheetsScope()
    {
        var tokenExchanger = new FakeGoogleSheetsOAuthTokenExchanger();
        using var factory = CreateConfiguredFactory(tokenExchanger);
        var state = CreateGoogleSheetsStateToken(factory.Services);
        var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/integrations/google/callback?code=auth-code&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/?integration=google-sheets&status=callback-received",
            response.Headers.Location?.OriginalString);
        Assert.Equal("auth-code", tokenExchanger.Code);
        Assert.Equal("http://localhost/integrations/google/callback", tokenExchanger.RedirectUri);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = await dbContext.GoogleConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TestAuthContext.UserId, connection.UserId);
        Assert.Equal(GoogleScopes.SpreadsheetsReadonly, connection.GrantedScopes);
    }

    [Fact]
    public async Task Callback_WhenExistingDriveConnection_MergesSheetsScope()
    {
        var tokenExchanger = new FakeGoogleSheetsOAuthTokenExchanger();
        using var factory = CreateConfiguredFactory(tokenExchanger);
        var connectionId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GoogleConnections.Add(new GoogleConnection
            {
                Id = connectionId,
                UserId = TestAuthContext.UserId,
                EncryptedAccessToken = "encrypted-access-token",
                EncryptedRefreshToken = "encrypted-refresh-token",
                AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                GrantedScopes = GoogleScopes.DriveFile,
                TokenType = "Bearer",
                ConnectedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var state = CreateGoogleSheetsStateToken(factory.Services);
        var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/integrations/google/callback?code=auth-code&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var assertionScope = factory.Services.CreateScope();
        var assertionDbContext = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = await assertionDbContext.GoogleConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(connectionId, connection.Id);
        Assert.Equal(
            $"{GoogleScopes.DriveFile} {GoogleScopes.SpreadsheetsReadonly}",
            connection.GrantedScopes);
    }

    [Fact]
    public async Task Callback_WhenSheetsScopeMissing_ReturnsValidationProblem()
    {
        var tokenExchanger = new FakeGoogleSheetsOAuthTokenExchanger
        {
            Response = new GoogleOAuthTokenResponse
            {
                AccessToken = "ya29.test",
                ExpiresIn = 3599,
                RefreshToken = "1//test",
                Scope = GoogleScopes.DriveFile,
                TokenType = "Bearer",
            }
        };
        using var factory = CreateConfiguredFactory(tokenExchanger);
        var state = CreateGoogleSheetsStateToken(factory.Services);
        var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/integrations/google/callback?code=auth-code&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Google authorization did not grant the required Sheets scope.",
            problem.GetProperty("errors").GetProperty("scope")[0].GetString());
    }

    [Fact]
    public async Task Disconnect_RemovesSheetsScopeAndKeepsDriveScope()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GoogleConnections.Add(new GoogleConnection
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                EncryptedAccessToken = "encrypted-access-token",
                EncryptedRefreshToken = "encrypted-refresh-token",
                AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                GrantedScopes = $"{GoogleScopes.DriveFile} {GoogleScopes.SpreadsheetsReadonly}",
                TokenType = "Bearer",
                ConnectedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _client.PostAsync("/integrations/google-sheets/disconnect", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var assertionScope = _factory.Services.CreateScope();
        var assertionDbContext = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = await assertionDbContext.GoogleConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GoogleScopes.DriveFile, connection.GrantedScopes);
        Assert.Null(connection.RevokedAtUtc);
        Assert.NotEmpty(connection.EncryptedAccessToken);
    }

    [Fact]
    public async Task DocsStatusAndDisconnect_PreserveOtherScopes()
    {
        await SeedGoogleConnectionAsync(_factory, $"{GoogleScopes.DocumentsReadonly} {GoogleScopes.SpreadsheetsReadonly}");
        var before = await _client.GetFromJsonAsync<JsonElement>("/auth/me", TestContext.Current.CancellationToken);
        Assert.True(before.GetProperty("isGoogleDocsConnected").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync("/integrations/google-docs/disconnect", null, TestContext.Current.CancellationToken)).StatusCode);
        var after = await _client.GetFromJsonAsync<JsonElement>("/auth/me", TestContext.Current.CancellationToken);
        Assert.False(after.GetProperty("isGoogleDocsConnected").GetBoolean());
        Assert.True(after.GetProperty("isGoogleSheetsConnected").GetBoolean());
    }

    [Fact]
    public async Task SheetsDisconnect_PreservesDocsScope()
    {
        await SeedGoogleConnectionAsync(_factory, $"{GoogleScopes.DocumentsReadonly} {GoogleScopes.SpreadsheetsReadonly}");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync("/integrations/google-sheets/disconnect", null, TestContext.Current.CancellationToken)).StatusCode);
        var after = await _client.GetFromJsonAsync<JsonElement>("/auth/me", TestContext.Current.CancellationToken);
        Assert.True(after.GetProperty("isGoogleDocsConnected").GetBoolean());
        Assert.False(after.GetProperty("isGoogleSheetsConnected").GetBoolean());
    }

    [Theory]
    [InlineData("/integrations/google-docs/disconnect", GoogleScopes.DocumentsReadonly)]
    [InlineData("/integrations/google-sheets/disconnect", GoogleScopes.SpreadsheetsReadonly)]
    public async Task Disconnect_LastScope_ClearsTokens(string path, string scopeValue)
    {
        await SeedGoogleConnectionAsync(_factory, scopeValue);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync(path, null, TestContext.Current.CancellationToken)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var connection = await scope.ServiceProvider.GetRequiredService<AppDbContext>().GoogleConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(connection.RevokedAtUtc);
        Assert.Empty(connection.EncryptedAccessToken);
        Assert.Empty(connection.EncryptedRefreshToken);
    }

    [Fact]
    public async Task Callback_WithoutState_ReturnsValidationProblem()
    {
        var response = await _client.GetAsync("/integrations/google/callback", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Google OAuth state is required.",
            problem.GetProperty("errors").GetProperty("state")[0].GetString());
    }

    [Theory]
    [InlineData("forged")]
    [InlineData("other-user")]
    [InlineData("unknown-integration")]
    [InlineData("expired")]
    public async Task Callback_RejectsInvalidState(string kind)
    {
        var state = kind switch
        {
            "forged" => "not-protected",
            "other-user" => CreateGoogleSheetsStateToken(_factory.Services, Guid.NewGuid()),
            "unknown-integration" => CreateGoogleSheetsStateToken(_factory.Services, integrationId: "drive"),
            _ => CreateGoogleSheetsStateToken(_factory.Services, lifetime: TimeSpan.FromSeconds(-1)),
        };
        var response = await _client.GetAsync($"/integrations/google/callback?code=test&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Contains("invalid or expired", problem.GetProperty("errors").GetProperty("state")[0].GetString());
    }

    [Fact]
    public async Task DocsCallback_WhenScopeMissing_ReturnsDocsSpecificError()
    {
        using var factory = CreateConfiguredFactory(new FakeGoogleSheetsOAuthTokenExchanger());
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var connect = await client.GetAsync("/integrations/google-docs/connect", TestContext.Current.CancellationToken);
        var state = QueryHelpers.ParseQuery(connect.Headers.Location!.Query)["state"].ToString();
        var callback = await client.GetAsync($"/integrations/google/callback?code=test&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, callback.StatusCode);
        var problem = await callback.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Google authorization did not grant the required Docs scope.", problem.GetProperty("errors").GetProperty("scope")[0].GetString());
    }

    [Fact]
    public async Task DocsCallback_WhenDenied_UsesDocsSpecificError()
    {
        using var factory = CreateConfiguredFactory(new FakeGoogleSheetsOAuthTokenExchanger());
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var connect = await client.GetAsync("/integrations/google-docs/connect", TestContext.Current.CancellationToken);
        var state = QueryHelpers.ParseQuery(connect.Headers.Location!.Query)["state"].ToString();
        var callback = await client.GetAsync($"/integrations/google/callback?error=access_denied&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, callback.StatusCode);
        var problem = await callback.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Google Docs connection was not approved.", problem.GetProperty("title").GetString());
    }

    private static string CreateGoogleSheetsStateToken(IServiceProvider services, Guid? userId = null, string integrationId = "google-sheets", TimeSpan? lifetime = null)
    {
        using var scope = services.CreateScope();
        var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider
            .CreateProtector("Glovelly.GoogleIntegrationOAuthState")
            .ToTimeLimitedDataProtector();

        return protector.Protect(
            JsonSerializer.Serialize(new
            {
                userId = userId ?? TestAuthContext.UserId,
                integrationId,
            }, JsonOptions),
            lifetime: lifetime ?? TimeSpan.FromMinutes(15));
    }

    private WebApplicationFactory<Program> CreateConfiguredFactory(
        FakeGoogleSheetsOAuthTokenExchanger tokenExchanger)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "google-client-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "google-client-secret");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleOAuthTokenClient>();
                services.AddSingleton<IGoogleOAuthTokenClient>(tokenExchanger);
            });
        });
    }

    private static async Task SeedGoogleConnectionAsync(WebApplicationFactory<Program> factory, string grantedScopes)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.GoogleConnections.Add(new GoogleConnection
        {
            Id = Guid.NewGuid(),
            UserId = TestAuthContext.UserId,
            EncryptedAccessToken = "encrypted-access-token",
            EncryptedRefreshToken = "encrypted-refresh-token",
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            GrantedScopes = grantedScopes,
            TokenType = "Bearer",
            ConnectedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class FakeGoogleSheetsOAuthTokenExchanger : IGoogleOAuthTokenClient
    {
        public string? Code { get; private set; }
        public string? RedirectUri { get; private set; }
        public GoogleOAuthTokenResponse Response { get; set; } = new()
        {
            AccessToken = "ya29.test",
            ExpiresIn = 3599,
            RefreshToken = "1//test",
            RefreshTokenExpiresIn = 604799,
            Scope = GoogleScopes.SpreadsheetsReadonly,
            TokenType = "Bearer",
        };

        public Task<GoogleOAuthTokenExchangeResult> ExchangeCodeAsync(
            string code,
            string redirectUri,
            string clientId,
            string clientSecret,
            CancellationToken cancellationToken)
        {
            Code = code;
            RedirectUri = redirectUri;

            return Task.FromResult(new GoogleOAuthTokenExchangeResult(
                true,
                StatusCodes.Status200OK,
                JsonSerializer.Serialize(Response, JsonOptions),
                Response));
        }

        public Task<GoogleOAuthTokenRefreshResult> RefreshAccessTokenAsync(
            string refreshToken,
            string clientId,
            string clientSecret,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
