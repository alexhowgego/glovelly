using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class InvoiceDeliveryEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public InvoiceDeliveryEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendEmail_WhenInvoiceHasPdfAndClientEmail_SendsAttachmentAndLogsDelivery()
    {
        var updateSettingsResponse = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = (string?)null,
            invoiceReplyToEmail = "alex@example.com",
        });
        updateSettingsResponse.EnsureSuccessStatusCode();

        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 invoice content");
        var createInvoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-SEND-001",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-04-20",
            dueDate = "2026-05-04",
            status = "Issued",
            description = "Email delivery test.",
            pdfBlob = Convert.ToBase64String(pdfBytes),
        });
        createInvoiceResponse.EnsureSuccessStatusCode();

        var createdInvoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var invoiceId = createdInvoice.GetProperty("id").GetGuid();

        var response = await _client.PostAsJsonAsync($"/invoices/{invoiceId}/send-email", new
        {
            message = "Please process this one this week.",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var message = Assert.Single(_factory.Emails.SentEmails);
        Assert.Equal("bookings@foxandfinch.co.uk", message.To.Single().Address);
        Assert.Equal("Fox & Finch Events", message.To.Single().DisplayName);
        Assert.Equal("Invoice GLV-SEND-001 from Glovelly", message.Subject);
        Assert.Equal("invoices@glovelly.test", message.From?.Address);
        Assert.Equal("Test Admin (via Glovelly)", message.From?.DisplayName);
        Assert.Equal("alex@example.com", message.ReplyTo?.Address);
        Assert.Equal("Test Admin", message.ReplyTo?.DisplayName);
        Assert.Contains("Please find invoice GLV-SEND-001 attached.", message.PlainTextBody);
        Assert.Contains("Please process this one this week.", message.PlainTextBody);

        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("GLV-SEND-001.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(pdfBytes, attachment.Content);

        Assert.Equal(1, updatedInvoice.GetProperty("deliveryCount").GetInt32());
        Assert.Equal("Email", updatedInvoice.GetProperty("lastDeliveryChannel").GetString());
        Assert.Equal("bookings@foxandfinch.co.uk", updatedInvoice.GetProperty("lastDeliveryRecipient").GetString());
        Assert.Equal(TestAuthContext.UserId, updatedInvoice.GetProperty("lastDeliveredByUserId").GetGuid());
        Assert.Equal(JsonValueKind.String, updatedInvoice.GetProperty("lastDeliveredUtc").ValueKind);
    }

    [Fact]
    public async Task SendEmail_WhenBodyOmitted_SendsStandardMessage()
    {
        var createInvoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-SEND-OPTIONAL-BODY",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-04-21",
            dueDate = "2026-05-05",
            status = "Issued",
            description = "Optional body delivery test.",
            pdfBlob = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4 invoice content")),
        });
        createInvoiceResponse.EnsureSuccessStatusCode();

        var invoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await _client.PostAsync(
            $"/invoices/{invoice.GetProperty("id").GetGuid()}/send-email",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = Assert.Single(_factory.Emails.SentEmails);
        Assert.Contains("Please find invoice GLV-SEND-OPTIONAL-BODY attached.", message.PlainTextBody);
        Assert.DoesNotContain("Message:", message.PlainTextBody);
        Assert.Null(message.ReplyTo);
    }

    [Fact]
    public async Task SendEmail_WhenClientEmailMissing_ReturnsValidationProblemWithoutSending()
    {
        var createClientResponse = await _client.PostAsJsonAsync("/clients", new
        {
            name = "No Email Client",
            email = "   ",
            billingAddress = new
            {
                line1 = "1 Test Street",
                city = "Leeds",
                stateOrCounty = "West Yorkshire",
                postalCode = "LS1 1AA",
                country = "United Kingdom",
            },
        });
        createClientResponse.EnsureSuccessStatusCode();
        var client = await createClientResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var createInvoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-SEND-002",
            clientId = client.GetProperty("id").GetGuid(),
            invoiceDate = "2026-04-20",
            dueDate = "2026-05-04",
            status = "Issued",
            description = "Missing email delivery test.",
            pdfBlob = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4 invoice content")),
        });
        createInvoiceResponse.EnsureSuccessStatusCode();
        var invoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await _client.PostAsJsonAsync($"/invoices/{invoice.GetProperty("id").GetGuid()}/send-email", new
        {
            message = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Emails.SentEmails);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Invoice recipient email is missing.",
            problem.GetProperty("errors").GetProperty("recipient")[0].GetString());
    }

    [Fact]
    public async Task SendEmail_WhenInvoicePdfMissing_ReturnsValidationProblemWithoutSending()
    {
        var response = await _client.PostAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/send-email", new
        {
            message = "Please pay this invoice.",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Emails.SentEmails);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Invoice PDF is missing.",
            problem.GetProperty("errors").GetProperty("pdf")[0].GetString());
    }

    [Fact]
    public async Task PublishGoogleDrive_WhenConnected_UploadsPdfAndLogsDelivery()
    {
        var driveClient = new FakeGoogleDriveApiClient();
        using var factory = CreateFactoryWithGoogleDriveClient(driveClient);
        var client = factory.CreateClient();
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 drive invoice content");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokenProtector = scope.ServiceProvider.GetRequiredService<IGoogleDriveTokenProtector>();
            dbContext.GoogleDriveConnections.Add(new GoogleDriveConnection
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                EncryptedAccessToken = tokenProtector.Protect("access-token"),
                EncryptedRefreshToken = tokenProtector.Protect("refresh-token"),
                AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                Scope = "https://www.googleapis.com/auth/drive.file",
                TokenType = "Bearer",
                ConnectedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        var createInvoiceResponse = await client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-DRIVE-001",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-04-20",
            dueDate = "2026-05-04",
            status = "Issued",
            description = "Drive delivery test.",
            pdfBlob = Convert.ToBase64String(pdfBytes),
        });
        createInvoiceResponse.EnsureSuccessStatusCode();
        var createdInvoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var invoiceId = createdInvoice.GetProperty("id").GetGuid();

        var response = await client.PostAsync(
            $"/invoices/{invoiceId}/publish/google-drive",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var publishResult = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var updatedInvoice = publishResult.GetProperty("invoice");

        Assert.Equal("access-token", driveClient.AccessToken);
        Assert.Equal("GLV-DRIVE-001.pdf", driveClient.FileName);
        Assert.Equal(pdfBytes, driveClient.Content);
        Assert.Null(driveClient.ParentFolderId);
        Assert.Equal("drive-file-id", publishResult.GetProperty("fileId").GetString());
        Assert.Equal("GLV-DRIVE-001.pdf", publishResult.GetProperty("fileName").GetString());
        Assert.Equal(
            "https://drive.google.com/file/d/drive-file-id/view",
            publishResult.GetProperty("webViewLink").GetString());
        Assert.Equal(1, updatedInvoice.GetProperty("deliveryCount").GetInt32());
        Assert.Equal("GoogleDrive", updatedInvoice.GetProperty("lastDeliveryChannel").GetString());
        Assert.Equal(
            "https://drive.google.com/file/d/drive-file-id/view",
            updatedInvoice.GetProperty("lastDeliveryRecipient").GetString());
        Assert.Equal(TestAuthContext.UserId, updatedInvoice.GetProperty("lastDeliveredByUserId").GetGuid());
    }

    [Fact]
    public async Task PublishGoogleDrive_WhenConnectionHasUploadFolder_UsesFolder()
    {
        var driveClient = new FakeGoogleDriveApiClient();
        using var factory = CreateFactoryWithGoogleDriveClient(driveClient);
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokenProtector = scope.ServiceProvider.GetRequiredService<IGoogleDriveTokenProtector>();
            dbContext.GoogleDriveConnections.Add(new GoogleDriveConnection
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                EncryptedAccessToken = tokenProtector.Protect("access-token"),
                EncryptedRefreshToken = tokenProtector.Protect("refresh-token"),
                AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                Scope = "https://www.googleapis.com/auth/drive.file",
                TokenType = "Bearer",
                InvoiceUploadFolderId = "drive-folder-id",
                ConnectedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        var createInvoiceResponse = await client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-DRIVE-FOLDER",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-04-20",
            dueDate = "2026-05-04",
            status = "Issued",
            description = "Drive folder delivery test.",
            pdfBlob = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4 invoice content")),
        });
        createInvoiceResponse.EnsureSuccessStatusCode();
        var createdInvoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await client.PostAsync(
            $"/invoices/{createdInvoice.GetProperty("id").GetGuid()}/publish/google-drive",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("drive-folder-id", driveClient.ParentFolderId);
    }

    [Fact]
    public async Task PublishGoogleDrive_WhenNotConnected_ReturnsProblem()
    {
        var driveClient = new FakeGoogleDriveApiClient();
        using var factory = CreateFactoryWithGoogleDriveClient(driveClient);
        var client = factory.CreateClient();
        var createInvoiceResponse = await client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-DRIVE-MISSING-CONNECTION",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-04-20",
            dueDate = "2026-05-04",
            status = "Issued",
            description = "Drive missing connection test.",
            pdfBlob = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4 invoice content")),
        });
        createInvoiceResponse.EnsureSuccessStatusCode();
        var createdInvoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await client.PostAsync(
            $"/invoices/{createdInvoice.GetProperty("id").GetGuid()}/publish/google-drive",
            content: null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(driveClient.FileName);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Unable to publish invoice to Google Drive", problem.GetProperty("title").GetString());
        Assert.Equal("Google Drive is not connected.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task PublishGoogleDrive_WhenAccessTokenExpired_RefreshesBeforeUpload()
    {
        var driveClient = new FakeGoogleDriveApiClient
        {
            RefreshedAccessToken = "new-access-token",
        };
        using var factory = CreateFactoryWithGoogleDriveClient(driveClient);
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokenProtector = scope.ServiceProvider.GetRequiredService<IGoogleDriveTokenProtector>();
            dbContext.GoogleDriveConnections.Add(new GoogleDriveConnection
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                EncryptedAccessToken = tokenProtector.Protect("expired-access-token"),
                EncryptedRefreshToken = tokenProtector.Protect("refresh-token"),
                AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
                Scope = "https://www.googleapis.com/auth/drive.file",
                TokenType = "Bearer",
                ConnectedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        var createInvoiceResponse = await client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-DRIVE-REFRESH",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-04-20",
            dueDate = "2026-05-04",
            status = "Issued",
            description = "Drive refresh test.",
            pdfBlob = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4 invoice content")),
        });
        createInvoiceResponse.EnsureSuccessStatusCode();
        var createdInvoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await client.PostAsync(
            $"/invoices/{createdInvoice.GetProperty("id").GetGuid()}/publish/google-drive",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("refresh-token", driveClient.RefreshToken);
        Assert.Equal("new-access-token", driveClient.AccessToken);

        using var assertionScope = factory.Services.CreateScope();
        var assertionDbContext = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assertionTokenProtector = assertionScope.ServiceProvider.GetRequiredService<IGoogleDriveTokenProtector>();
        var connection = await assertionDbContext.GoogleDriveConnections.SingleAsync();
        Assert.Equal("new-access-token", assertionTokenProtector.Unprotect(connection.EncryptedAccessToken));
    }

    private WebApplicationFactory<Program> CreateFactoryWithGoogleDriveClient(
        FakeGoogleDriveApiClient driveClient)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "google-client-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "google-client-secret");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleDriveApiClient>();
                services.AddSingleton<IGoogleDriveApiClient>(driveClient);
            });
        });
    }

    private sealed class FakeGoogleDriveApiClient : IGoogleDriveApiClient
    {
        public string? AccessToken { get; private set; }
        public string? ContentText { get; private set; }
        public byte[]? Content { get; private set; }
        public string? FileName { get; private set; }
        public string? ParentFolderId { get; private set; }
        public string? RefreshToken { get; private set; }
        public string RefreshedAccessToken { get; set; } = "refreshed-access-token";

        public Task<GoogleDriveAccessTokenRefreshResult> RefreshAccessTokenAsync(
            string refreshToken,
            string clientId,
            string clientSecret,
            CancellationToken cancellationToken)
        {
            RefreshToken = refreshToken;
            var tokenResponse = new GoogleDriveOAuthTokenResponse
            {
                AccessToken = RefreshedAccessToken,
                ExpiresIn = 3599,
                Scope = "https://www.googleapis.com/auth/drive.file",
                TokenType = "Bearer",
            };

            return Task.FromResult(new GoogleDriveAccessTokenRefreshResult(
                true,
                StatusCodes.Status200OK,
                "{}",
                tokenResponse));
        }

        public Task<GoogleDriveUploadResult> UploadPdfAsync(
            string accessToken,
            string fileName,
            byte[] content,
            string? parentFolderId,
            CancellationToken cancellationToken)
        {
            AccessToken = accessToken;
            FileName = fileName;
            Content = content;
            ContentText = Encoding.ASCII.GetString(content);
            ParentFolderId = parentFolderId;

            return Task.FromResult(new GoogleDriveUploadResult(
                "drive-file-id",
                fileName,
                "https://drive.google.com/file/d/drive-file-id/view"));
        }
    }
}
