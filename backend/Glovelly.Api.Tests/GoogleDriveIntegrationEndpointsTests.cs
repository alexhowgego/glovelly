using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Glovelly.Api.Services;
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

public sealed class GoogleDriveIntegrationEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public GoogleDriveIntegrationEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Connect_RedirectsToGoogleAuthorizationEndpoint()
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

        var response = await client.GetAsync("/integrations/google-drive/connect");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("https", location.Scheme);
        Assert.Equal("accounts.google.com", location.Host);
        Assert.Equal("/o/oauth2/v2/auth", location.AbsolutePath);

        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("google-client-id", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("https://www.googleapis.com/auth/drive.file", query["scope"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("consent", query["prompt"]);
        Assert.Equal(
            "http://localhost/integrations/google-drive/callback",
            query["redirect_uri"]);
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
    }

    [Fact]
    public async Task Callback_WithCodeAndState_RedirectsToIntegrationStatus()
    {
        var tokenExchanger = new FakeGoogleDriveOAuthTokenExchanger();
        using var factory = CreateConfiguredFactory(tokenExchanger);
        var state = CreateGoogleDriveStateToken(factory.Services);
        var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/integrations/google-drive/callback?code=auth-code&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/?integration=google-drive&status=callback-received",
            response.Headers.Location?.OriginalString);
        Assert.Equal("auth-code", tokenExchanger.Code);
        Assert.Equal("http://localhost/integrations/google-drive/callback", tokenExchanger.RedirectUri);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = await dbContext.GoogleDriveConnections.SingleAsync();
        Assert.Equal(TestAuthContext.UserId, connection.UserId);
        Assert.Equal("ya29.test", connection.AccessToken);
        Assert.Equal("1//test", connection.RefreshToken);
        Assert.Equal("https://www.googleapis.com/auth/drive.file", connection.Scope);
        Assert.Equal("Bearer", connection.TokenType);
        Assert.True(connection.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.True(connection.RefreshTokenExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.Null(connection.RevokedAtUtc);
    }

    [Fact]
    public async Task Callback_WithoutNewRefreshToken_PreservesExistingRefreshToken()
    {
        var tokenExchanger = new FakeGoogleDriveOAuthTokenExchanger
        {
            Response = new GoogleDriveOAuthTokenResponse
            {
                AccessToken = "ya29.new",
                ExpiresIn = 1800,
                RefreshToken = null,
                Scope = "https://www.googleapis.com/auth/drive.file",
                TokenType = "Bearer",
            }
        };
        using var factory = CreateConfiguredFactory(tokenExchanger);
        var previousRefreshExpiry = DateTimeOffset.UtcNow.AddDays(5);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GoogleDriveConnections.Add(new GoogleDriveConnection
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                AccessToken = "ya29.old",
                RefreshToken = "1//existing",
                AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10),
                RefreshTokenExpiresAtUtc = previousRefreshExpiry,
                Scope = "https://www.googleapis.com/auth/drive.file",
                TokenType = "Bearer",
                ConnectedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            });
            await dbContext.SaveChangesAsync();
        }

        var state = CreateGoogleDriveStateToken(factory.Services);
        var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/integrations/google-drive/callback?code=auth-code&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var assertionScope = factory.Services.CreateScope();
        var assertionDbContext = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = await assertionDbContext.GoogleDriveConnections.SingleAsync();
        Assert.Equal("ya29.new", connection.AccessToken);
        Assert.Equal("1//existing", connection.RefreshToken);
        Assert.Equal(previousRefreshExpiry, connection.RefreshTokenExpiresAtUtc);
    }

    [Fact]
    public async Task Callback_WithoutState_ReturnsValidationProblem()
    {
        var response = await _client.GetAsync("/integrations/google-drive/callback");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Google Drive OAuth state is required.",
            problem.GetProperty("errors").GetProperty("state")[0].GetString());
    }

    [Fact]
    public async Task Callback_WithInvalidState_ReturnsValidationProblem()
    {
        var response = await _client.GetAsync(
            "/integrations/google-drive/callback?code=auth-code&state=not-a-state-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Google Drive OAuth state is invalid or expired.",
            problem.GetProperty("errors").GetProperty("state")[0].GetString());
    }

    [Fact]
    public async Task Callback_WithStateButNoCode_ReturnsValidationProblem()
    {
        var state = CreateGoogleDriveStateToken();

        var response = await _client.GetAsync(
            $"/integrations/google-drive/callback?state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Google Drive authorization code is required.",
            problem.GetProperty("errors").GetProperty("code")[0].GetString());
    }

    [Fact]
    public async Task Callback_WithGoogleError_ReturnsProblem()
    {
        var state = CreateGoogleDriveStateToken();

        var response = await _client.GetAsync(
            $"/integrations/google-drive/callback?error=access_denied&error_description=User%20cancelled&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Google Drive connection was not approved.", problem.GetProperty("title").GetString());
        Assert.Equal("User cancelled", problem.GetProperty("detail").GetString());
    }

    private string CreateGoogleDriveStateToken(Guid? userId = null)
    {
        return CreateGoogleDriveStateToken(_factory.Services, userId);
    }

    private static string CreateGoogleDriveStateToken(IServiceProvider services, Guid? userId = null)
    {
        using var scope = services.CreateScope();
        var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider
            .CreateProtector("Glovelly.GoogleDriveOAuthState")
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
        FakeGoogleDriveOAuthTokenExchanger tokenExchanger)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "google-client-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "google-client-secret");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleDriveOAuthTokenExchanger>();
                services.AddSingleton<IGoogleDriveOAuthTokenExchanger>(tokenExchanger);
            });
        });
    }

    private sealed class FakeGoogleDriveOAuthTokenExchanger : IGoogleDriveOAuthTokenExchanger
    {
        public string? Code { get; private set; }
        public string? RedirectUri { get; private set; }
        public GoogleDriveOAuthTokenResponse Response { get; set; } = new()
        {
            AccessToken = "ya29.test",
            ExpiresIn = 3599,
            RefreshToken = "1//test",
            RefreshTokenExpiresIn = 604799,
            Scope = "https://www.googleapis.com/auth/drive.file",
            TokenType = "Bearer",
        };

        public Task<GoogleDriveOAuthTokenExchangeResult> ExchangeCodeAsync(
            string code,
            string redirectUri,
            string clientId,
            string clientSecret,
            CancellationToken cancellationToken)
        {
            Code = code;
            RedirectUri = redirectUri;

            return Task.FromResult(new GoogleDriveOAuthTokenExchangeResult(
                true,
                StatusCodes.Status200OK,
                JsonSerializer.Serialize(Response, JsonOptions),
                Response));
        }
    }
}
