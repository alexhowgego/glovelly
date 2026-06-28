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
        Assert.Equal("http://localhost/integrations/google-sheets/callback", query["redirect_uri"]);
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
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
            $"/integrations/google-sheets/callback?code=auth-code&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/?integration=google-sheets&status=callback-received",
            response.Headers.Location?.OriginalString);
        Assert.Equal("auth-code", tokenExchanger.Code);
        Assert.Equal("http://localhost/integrations/google-sheets/callback", tokenExchanger.RedirectUri);

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
            $"/integrations/google-sheets/callback?code=auth-code&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);

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
            $"/integrations/google-sheets/callback?code=auth-code&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);

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
    public async Task Callback_WithoutState_ReturnsValidationProblem()
    {
        var response = await _client.GetAsync("/integrations/google-sheets/callback", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Google Sheets OAuth state is required.",
            problem.GetProperty("errors").GetProperty("state")[0].GetString());
    }

    private static string CreateGoogleSheetsStateToken(IServiceProvider services, Guid? userId = null)
    {
        using var scope = services.CreateScope();
        var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider
            .CreateProtector("Glovelly.GoogleSheetsOAuthState")
            .ToTimeLimitedDataProtector();

        return protector.Protect(
            JsonSerializer.Serialize(new
            {
                userId = userId ?? TestAuthContext.UserId,
                createdUtc = DateTime.UtcNow,
            }, JsonOptions),
            lifetime: TimeSpan.FromMinutes(15));
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
