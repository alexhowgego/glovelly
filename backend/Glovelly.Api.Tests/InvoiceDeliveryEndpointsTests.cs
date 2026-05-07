using System.IO.Compression;
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
            invoiceEmailSubjectPattern = (string?)null,
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
    public async Task SendEmail_WhenReceiptInclusionRequested_AttachesReceiptZip()
    {
        var (invoiceId, receiptBytes) = await SeedInvoiceWithReceiptAsync(
            "GLV-SEND-RECEIPTS",
            "Taxi - airport",
            "taxi/receipt.jpg",
            "image/jpeg",
            Encoding.ASCII.GetBytes("receipt image bytes"));

        var response = await _client.PostAsJsonAsync($"/invoices/{invoiceId}/send-email", new
        {
            message = "Receipt attached too.",
            includeReceipts = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = Assert.Single(_factory.Emails.SentEmails);
        Assert.Equal(2, message.Attachments.Count);
        var pdfAttachment = Assert.Single(message.Attachments, attachment => attachment.ContentType == "application/pdf");
        Assert.Equal("GLV-SEND-RECEIPTS.pdf", pdfAttachment.FileName);

        var zipAttachment = Assert.Single(message.Attachments, attachment => attachment.ContentType == "application/zip");
        Assert.Equal("Invoice-GLV-SEND-RECEIPTS-Receipts.zip", zipAttachment.FileName);
        using var zipStream = new MemoryStream(zipAttachment.Content);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = Assert.Single(archive.Entries);
        Assert.Equal("Taxi - airport-taxi-receipt.jpg", entry.FullName);
        using var entryStream = entry.Open();
        using var entryMemory = new MemoryStream();
        await entryStream.CopyToAsync(entryMemory);
        Assert.Equal(receiptBytes, entryMemory.ToArray());
    }

    [Fact]
    public async Task SendEmail_WhenReceiptInclusionOmitted_DoesNotAttachReceiptZip()
    {
        var (invoiceId, _) = await SeedInvoiceWithReceiptAsync(
            "GLV-SEND-NO-RECEIPTS",
            "Parking",
            "parking.pdf",
            "application/pdf",
            Encoding.ASCII.GetBytes("%PDF-1.4 receipt"));

        var response = await _client.PostAsJsonAsync($"/invoices/{invoiceId}/send-email", new
        {
            message = "No receipt pack please.",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = Assert.Single(_factory.Emails.SentEmails);
        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("GLV-SEND-NO-RECEIPTS.pdf", attachment.FileName);
    }

    [Fact]
    public async Task SendEmail_WhenReceiptPackExceedsConfiguredLimit_ReturnsValidationProblem()
    {
        using var factory = CreateFactoryWithEmailAttachmentLimit(maxTotalAttachmentBytes: 32);
        var client = factory.CreateClient();
        var (invoiceId, _) = await SeedInvoiceWithReceiptAsync(
            factory,
            "GLV-SEND-TOO-LARGE",
            "Hotel",
            "hotel.pdf",
            "application/pdf",
            Encoding.ASCII.GetBytes("%PDF-1.4 oversized receipt content"));

        var response = await client.PostAsJsonAsync($"/invoices/{invoiceId}/send-email", new
        {
            includeReceipts = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Emails.SentEmails);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Contains(
            "exceeding the configured",
            problem.GetProperty("errors").GetProperty("attachments")[0].GetString());
    }

    [Fact]
    public async Task SendEmail_WithUserSubjectPattern_UsesResolvedSubject()
    {
        var updateSettingsResponse = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = (string?)null,
            invoiceEmailSubjectPattern = "{ClientName} invoice {InvoiceNumber}",
            invoiceReplyToEmail = (string?)null,
        });
        updateSettingsResponse.EnsureSuccessStatusCode();

        var createInvoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-SUBJECT-001",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-04-20",
            dueDate = "2026-05-04",
            status = "Issued",
            description = "Subject pattern test.",
            pdfBlob = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4 invoice content")),
        });
        createInvoiceResponse.EnsureSuccessStatusCode();
        var invoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await _client.PostAsync(
            $"/invoices/{invoice.GetProperty("id").GetGuid()}/send-email",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = Assert.Single(_factory.Emails.SentEmails);
        Assert.Equal("Fox & Finch Events invoice GLV-SUBJECT-001", message.Subject);
    }

    [Fact]
    public async Task SendEmail_WithClientSubjectPattern_OverridesUserSubjectPattern()
    {
        var updateSettingsResponse = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = (string?)null,
            invoiceEmailSubjectPattern = "User invoice {InvoiceNumber}",
            invoiceReplyToEmail = (string?)null,
        });
        updateSettingsResponse.EnsureSuccessStatusCode();

        var updateClientResponse = await _client.PutAsJsonAsync($"/clients/{TestData.FoxAndFinchId}", new
        {
            id = TestData.FoxAndFinchId,
            name = "Fox & Finch Events",
            email = "bookings@foxandfinch.co.uk",
            billingAddress = new
            {
                line1 = "12 Chapel Street",
                city = "Manchester",
                stateOrCounty = "Greater Manchester",
                postalCode = "M3 5JZ",
                country = "United Kingdom",
            },
            mileageRate = 0.52m,
            passengerMileageRate = 0.15m,
            invoiceFilenamePattern = (string?)null,
            invoiceEmailSubjectPattern = "Client invoice {InvoiceNumber} for {ClientName}",
        });
        updateClientResponse.EnsureSuccessStatusCode();

        var createInvoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-SUBJECT-002",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-04-20",
            dueDate = "2026-05-04",
            status = "Issued",
            description = "Client subject pattern test.",
            pdfBlob = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4 invoice content")),
        });
        createInvoiceResponse.EnsureSuccessStatusCode();
        var invoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await _client.PostAsync(
            $"/invoices/{invoice.GetProperty("id").GetGuid()}/send-email",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = Assert.Single(_factory.Emails.SentEmails);
        Assert.Equal("Client invoice GLV-SUBJECT-002 for Fox & Finch Events", message.Subject);
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

    private WebApplicationFactory<Program> CreateFactoryWithEmailAttachmentLimit(long maxTotalAttachmentBytes)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<EmailSettings>(settings =>
                {
                    settings.MaxTotalAttachmentBytes = maxTotalAttachmentBytes;
                });
            });
        });
    }

    private Task<(Guid InvoiceId, byte[] ReceiptBytes)> SeedInvoiceWithReceiptAsync(
        string invoiceNumber,
        string expenseDescription,
        string receiptFileName,
        string receiptContentType,
        byte[] receiptBytes)
    {
        return SeedInvoiceWithReceiptAsync(
            _factory,
            invoiceNumber,
            expenseDescription,
            receiptFileName,
            receiptContentType,
            receiptBytes);
    }

    private static async Task<(Guid InvoiceId, byte[] ReceiptBytes)> SeedInvoiceWithReceiptAsync(
        WebApplicationFactory<Program> factory,
        string invoiceNumber,
        string expenseDescription,
        string receiptFileName,
        string receiptContentType,
        byte[] receiptBytes)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attachmentStore = scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
        var invoiceId = Guid.NewGuid();
        var gigId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var storageKey = $"tests/{attachmentId}";

        await using (var receiptStream = new MemoryStream(receiptBytes))
        {
            await attachmentStore.SaveAsync(storageKey, receiptStream, receiptContentType);
        }

        dbContext.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            InvoiceNumber = invoiceNumber,
            ClientId = TestData.FoxAndFinchId,
            InvoiceDate = new DateOnly(2026, 4, 20),
            DueDate = new DateOnly(2026, 5, 4),
            Status = InvoiceStatus.Issued,
            Description = "Receipt delivery test.",
            PdfBlob = Encoding.ASCII.GetBytes("%PDF-1.4 invoice content"),
            CreatedByUserId = TestAuthContext.UserId,
            UpdatedByUserId = TestAuthContext.UserId,
        });
        dbContext.Gigs.Add(new Gig
        {
            Id = gigId,
            ClientId = TestData.FoxAndFinchId,
            InvoiceId = invoiceId,
            Title = "Receipt gig",
            Date = new DateOnly(2026, 4, 19),
            Venue = "Test venue",
            Status = GigStatus.Confirmed,
            CreatedByUserId = TestAuthContext.UserId,
            UpdatedByUserId = TestAuthContext.UserId,
            Expenses =
            [
                new GigExpense
                {
                    Id = expenseId,
                    SortOrder = 1,
                    Description = expenseDescription,
                    Amount = 42m,
                    Attachments =
                    [
                        new ExpenseAttachment
                        {
                            Id = attachmentId,
                            FileName = receiptFileName,
                            ContentType = receiptContentType,
                            SizeBytes = receiptBytes.Length,
                            StorageKey = storageKey,
                            CreatedAt = DateTimeOffset.UtcNow,
                        }
                    ],
                }
            ],
        });
        dbContext.InvoiceLines.Add(new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            SortOrder = 1,
            Type = InvoiceLineType.MiscExpense,
            Description = expenseDescription,
            Quantity = 1m,
            UnitPrice = 42m,
            GigId = gigId,
            IsSystemGenerated = true,
            CreatedByUserId = TestAuthContext.UserId,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        return (invoiceId, receiptBytes);
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
