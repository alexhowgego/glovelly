using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AuthEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenAuthenticatedClaimUserIsUnknown()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUserInvoiceFilenamePattern()
    {
        var updateResponse = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            defaultPaymentWindowDays = 30,
            invoiceFilenamePattern = "{MonthName} {Year} {InvoiceNumber}",
            invoiceEmailSubjectPattern = "{ClientName} invoice {InvoiceNumber}",
            invoiceReplyToEmail = "billing@example.com",
        });
        updateResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "{MonthName} {Year} {InvoiceNumber}",
            payload.GetProperty("invoiceFilenamePattern").GetString());
        Assert.Equal(
            "{ClientName} invoice {InvoiceNumber}",
            payload.GetProperty("invoiceEmailSubjectPattern").GetString());
        Assert.Equal(
            "billing@example.com",
            payload.GetProperty("invoiceReplyToEmail").GetString());
        Assert.Equal(30, payload.GetProperty("defaultPaymentWindowDays").GetInt32());
        Assert.False(payload.GetProperty("isGoogleDriveConnected").GetBoolean());
    }

    [Fact]
    public async Task Me_ReturnsGoogleDriveConnected_WhenActiveConnectionExists()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GoogleDriveConnections.Add(new GoogleDriveConnection
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                EncryptedAccessToken = "encrypted-access-token",
                EncryptedRefreshToken = "encrypted-refresh-token",
                AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                Scope = "https://www.googleapis.com/auth/drive.file",
                TokenType = "Bearer",
                ConnectedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(payload.GetProperty("isGoogleDriveConnected").GetBoolean());
    }

    [Fact]
    public async Task UpdateSettings_WhenGoogleDriveConnected_PersistsInvoiceUploadFolderId()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GoogleDriveConnections.Add(new GoogleDriveConnection
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                EncryptedAccessToken = "encrypted-access-token",
                EncryptedRefreshToken = "encrypted-refresh-token",
                AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                Scope = "https://www.googleapis.com/auth/drive.file",
                TokenType = "Bearer",
                ConnectedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            defaultPaymentWindowDays = 14,
            invoiceFilenamePattern = "{InvoiceNumber}",
            invoiceReplyToEmail = (string?)null,
            invoiceUploadFolderId = "  drive-folder-id  ",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("drive-folder-id", payload.GetProperty("invoiceUploadFolderId").GetString());

        var meResponse = await _client.GetAsync("/auth/me");
        var mePayload = await meResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("drive-folder-id", mePayload.GetProperty("invoiceUploadFolderId").GetString());
    }

    [Fact]
    public async Task UpdateSettings_WithInvoiceUploadFolderIdWhenGoogleDriveDisconnected_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            defaultPaymentWindowDays = 14,
            invoiceFilenamePattern = "{InvoiceNumber}",
            invoiceReplyToEmail = (string?)null,
            invoiceUploadFolderId = "drive-folder-id",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Connect Google Drive before setting an invoice upload folder.",
            problem.GetProperty("errors").GetProperty("invoiceUploadFolderId")[0].GetString());
    }

    [Fact]
    public async Task UpdateSettings_PersistsInvoiceFilenamePatternAndReplyToEmail()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            defaultPaymentWindowDays = 21,
            invoiceFilenamePattern = "  {ClientName} {InvoiceNumber}  ",
            invoiceEmailSubjectPattern = "  Invoice {InvoiceNumber} for {ClientName}  ",
            invoiceReplyToEmail = "  accounts@example.com  ",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "{ClientName} {InvoiceNumber}",
            payload.GetProperty("invoiceFilenamePattern").GetString());
        Assert.Equal(
            "Invoice {InvoiceNumber} for {ClientName}",
            payload.GetProperty("invoiceEmailSubjectPattern").GetString());
        Assert.Equal(
            "accounts@example.com",
            payload.GetProperty("invoiceReplyToEmail").GetString());
        Assert.Equal(21, payload.GetProperty("defaultPaymentWindowDays").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedUser = await dbContext.Users.FindAsync(TestAuthContext.UserId);

        Assert.NotNull(savedUser);
        Assert.Equal("{ClientName} {InvoiceNumber}", savedUser.InvoiceFilenamePattern);
        Assert.Equal("Invoice {InvoiceNumber} for {ClientName}", savedUser.InvoiceEmailSubjectPattern);
        Assert.Equal("accounts@example.com", savedUser.InvoiceReplyToEmail);
        Assert.Equal(21, savedUser.DefaultPaymentWindowDays);
    }

    [Fact]
    public async Task UpdateSettings_WithWhitespaceOnlyInvoiceEmailSubjectPattern_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "{InvoiceNumber}",
            invoiceEmailSubjectPattern = "   ",
            invoiceReplyToEmail = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Invoice email subject pattern cannot be empty or whitespace.",
            problem.GetProperty("errors").GetProperty("invoiceEmailSubjectPattern")[0].GetString());
    }

    [Fact]
    public async Task UpdateSettings_WithWhitespaceOnlyInvoiceFilenamePattern_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "   ",
            invoiceReplyToEmail = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Invoice filename pattern cannot be empty or whitespace.",
            problem.GetProperty("errors").GetProperty("invoiceFilenamePattern")[0].GetString());
    }

    [Fact]
    public async Task UpdateSettings_WithInvalidReplyToEmail_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "{InvoiceNumber}",
            invoiceReplyToEmail = "not-an-email",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Reply-to email must be a valid email address.",
            problem.GetProperty("errors").GetProperty("invoiceReplyToEmail")[0].GetString());
    }

    [Fact]
    public async Task DeniedPage_ShowsRequestAccessUi_WhenSignedRequestTokenProvided()
    {
        var token = CreateAccessRequestToken("new-user@glovelly.local", "New User", "google-sub-new-user");

        var response = await _client.GetAsync($"/auth/denied?code=not_authorized&request={Uri.EscapeDataString(token)}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Request access", html);
        Assert.Contains("data-access-request-button", html);
    }

    private string CreateAccessRequestToken(string email, string displayName, string subject)
    {
        using var scope = _factory.Services.CreateScope();
        var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider
            .CreateProtector("Glovelly.AccessRequest")
            .ToTimeLimitedDataProtector();

        return protector.Protect(
            JsonSerializer.Serialize(new
            {
                email,
                displayName,
                subject,
            }),
            lifetime: TimeSpan.FromMinutes(15));
    }
}
