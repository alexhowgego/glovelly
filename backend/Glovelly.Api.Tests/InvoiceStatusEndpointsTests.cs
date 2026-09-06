using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class InvoiceStatusEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public InvoiceStatusEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateStatus_WhenTransitionAllowed_PersistsStatusAndAuditFields()
    {
        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Paid",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Paid", updatedInvoice.GetProperty("status").GetString());
        Assert.Equal(TestAuthContext.UserId, updatedInvoice.GetProperty("updatedByUserId").GetGuid());
        Assert.Equal(JsonValueKind.String, updatedInvoice.GetProperty("statusUpdatedUtc").ValueKind);
        Assert.Equal("2026-01-01", updatedInvoice.GetProperty("paidOn").GetString());
    }

    [Fact]
    public async Task UpdateStatus_WhenTransitionNotAllowed_ReturnsValidationProblem()
    {
        var makePaidResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Paid",
        }, TestContext.Current.CancellationToken);
        makePaidResponse.EnsureSuccessStatusCode();

        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Cancelled",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Invoice status cannot move from Paid to Cancelled.",
            problem.GetProperty("errors").GetProperty("status")[0].GetString());
    }

    [Fact]
    public async Task UpdateStatus_WhenOverdueCannotReturnDirectlyToIssued_ReturnsValidationProblem()
    {
        var makeOverdueResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Overdue",
        }, TestContext.Current.CancellationToken);
        makeOverdueResponse.EnsureSuccessStatusCode();

        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Issued",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Invoice status cannot move from Overdue to Issued.",
            problem.GetProperty("errors").GetProperty("status")[0].GetString());
    }

    [Fact]
    public async Task UpdateStatus_WhenInvoiceHasLines_ResponseKeepsLineTotals()
    {
        var createLineResponse = await _client.PostAsJsonAsync("/invoice-lines", new
        {
            invoiceId = TestData.FoxInvoiceId,
            sortOrder = 1,
            type = InvoiceLineType.PerformanceFee,
            description = "Headline performance",
            quantity = 2m,
            unitPrice = 150m,
        }, TestContext.Current.CancellationToken);
        createLineResponse.EnsureSuccessStatusCode();

        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Paid",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(300m, updatedInvoice.GetProperty("total").GetDecimal());
        Assert.Single(updatedInvoice.GetProperty("lines").EnumerateArray());
    }

    [Fact]
    public async Task UpdateStatus_WhenFirstIssued_StampsFirstIssueAndSlidesInvoiceDates()
    {
        var expectedInvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/status", new
        {
            status = "Issued",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Issued", updatedInvoice.GetProperty("status").GetString());
        Assert.Equal(expectedInvoiceDate.ToString("yyyy-MM-dd"), updatedInvoice.GetProperty("invoiceDate").GetString());
        Assert.Equal(expectedInvoiceDate.AddDays(14).ToString("yyyy-MM-dd"), updatedInvoice.GetProperty("dueDate").GetString());
        Assert.Equal(JsonValueKind.String, updatedInvoice.GetProperty("firstIssuedUtc").ValueKind);
        Assert.Equal(TestAuthContext.UserId, updatedInvoice.GetProperty("firstIssuedByUserId").GetGuid());
        Assert.False(updatedInvoice.TryGetProperty("pdfBlob", out _));
        Assert.Equal("application/pdf", updatedInvoice.GetProperty("pdfContentType").GetString());
        Assert.True(updatedInvoice.GetProperty("pdfSizeBytes").GetInt64() > 0);
        Assert.Contains(
            $"users/{TestAuthContext.UserId:N}/",
            updatedInvoice.GetProperty("pdfStorageKey").GetString());
        Assert.Contains(
            $"/invoices/{TestData.RiversideInvoiceId:D}/invoice.pdf",
            updatedInvoice.GetProperty("pdfStorageKey").GetString());
    }

    [Fact]
    public async Task UpdateStatus_WhenFirstIssued_UsesUserDefaultPaymentWindow()
    {
        var updateSettingsResponse = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            displayName = "Test Admin",
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            defaultPaymentWindowDays = 30,
            invoiceFilenamePattern = "{InvoiceNumber}",
            invoiceReplyToEmail = (string?)null,
        }, TestContext.Current.CancellationToken);
        updateSettingsResponse.EnsureSuccessStatusCode();

        var expectedInvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/status", new
        {
            status = "Issued",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            expectedInvoiceDate.AddDays(30).ToString("yyyy-MM-dd"),
            updatedInvoice.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task Reissue_WhenInvoiceExists_RegeneratesPdfAndLogsActionWithoutChangingFinancials()
    {
        var expectedInvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var createLineResponse = await _client.PostAsJsonAsync("/invoice-lines", new
        {
            invoiceId = TestData.RiversideInvoiceId,
            sortOrder = 1,
            type = InvoiceLineType.PerformanceFee,
            description = "Headline performance",
            quantity = 2m,
            unitPrice = 150m,
        }, TestContext.Current.CancellationToken);
        createLineResponse.EnsureSuccessStatusCode();

        var markIssuedResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/status", new
        {
            status = "Issued",
        }, TestContext.Current.CancellationToken);
        markIssuedResponse.EnsureSuccessStatusCode();

        var markPaidResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/status", new
        {
            status = "Paid",
        }, TestContext.Current.CancellationToken);
        markPaidResponse.EnsureSuccessStatusCode();

        var response = await _client.PostAsync($"/invoices/{TestData.RiversideInvoiceId}/reissue", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Draft", updatedInvoice.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, updatedInvoice.GetProperty("paidOn").ValueKind);
        Assert.Equal(JsonValueKind.String, updatedInvoice.GetProperty("statusUpdatedUtc").ValueKind);
        Assert.Equal(expectedInvoiceDate.ToString("yyyy-MM-dd"), updatedInvoice.GetProperty("invoiceDate").GetString());
        Assert.Equal(expectedInvoiceDate.AddDays(14).ToString("yyyy-MM-dd"), updatedInvoice.GetProperty("dueDate").GetString());
        Assert.Equal(300m, updatedInvoice.GetProperty("total").GetDecimal());
        Assert.Equal(1, updatedInvoice.GetProperty("reissueCount").GetInt32());
        Assert.Equal(JsonValueKind.String, updatedInvoice.GetProperty("firstIssuedUtc").ValueKind);
        Assert.Equal(TestAuthContext.UserId, updatedInvoice.GetProperty("firstIssuedByUserId").GetGuid());
        Assert.Equal(TestAuthContext.UserId, updatedInvoice.GetProperty("lastReissuedByUserId").GetGuid());
        Assert.Equal(JsonValueKind.String, updatedInvoice.GetProperty("lastReissuedUtc").ValueKind);
        Assert.False(updatedInvoice.TryGetProperty("pdfBlob", out _));
        Assert.Equal("application/pdf", updatedInvoice.GetProperty("pdfContentType").GetString());
        Assert.True(updatedInvoice.GetProperty("pdfSizeBytes").GetInt64() > 0);
        Assert.Single(updatedInvoice.GetProperty("lines").EnumerateArray());
    }

    [Fact]
    public async Task Reissue_WhenInvoiceIsDraft_ReturnsValidationProblem()
    {
        var response = await _client.PostAsync($"/invoices/{TestData.RiversideInvoiceId}/reissue", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Draft invoices can be redrafted, but cannot be re-issued until they have been issued.",
            problem.GetProperty("errors").GetProperty("status")[0].GetString());
    }

    [Fact]
    public async Task Reissue_WhenInvoiceIsCancelled_ReturnsValidationProblem()
    {
        var cancelResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Cancelled",
        }, TestContext.Current.CancellationToken);
        cancelResponse.EnsureSuccessStatusCode();

        var response = await _client.PostAsync($"/invoices/{TestData.FoxInvoiceId}/reissue", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Cancelled invoices must be moved back to Draft before they can be redrafted.",
            problem.GetProperty("errors").GetProperty("status")[0].GetString());
    }

    [Fact]
    public async Task Redraft_WhenInvoiceIsDraft_RegeneratesPdfWithoutIncrementingReissueAudit()
    {
        var expectedInvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await _client.PostAsync($"/invoices/{TestData.RiversideInvoiceId}/redraft", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Draft", updatedInvoice.GetProperty("status").GetString());
        Assert.Equal(expectedInvoiceDate.ToString("yyyy-MM-dd"), updatedInvoice.GetProperty("invoiceDate").GetString());
        Assert.Equal(expectedInvoiceDate.AddDays(14).ToString("yyyy-MM-dd"), updatedInvoice.GetProperty("dueDate").GetString());
        Assert.Equal(0, updatedInvoice.GetProperty("reissueCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, updatedInvoice.GetProperty("firstIssuedUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, updatedInvoice.GetProperty("firstIssuedByUserId").ValueKind);
        Assert.Equal(JsonValueKind.Null, updatedInvoice.GetProperty("lastReissuedUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, updatedInvoice.GetProperty("lastReissuedByUserId").ValueKind);
        Assert.False(updatedInvoice.TryGetProperty("pdfBlob", out _));
        Assert.Equal("application/pdf", updatedInvoice.GetProperty("pdfContentType").GetString());
        Assert.True(updatedInvoice.GetProperty("pdfSizeBytes").GetInt64() > 0);
    }

    [Fact]
    public async Task UpdateDescription_WhenDraft_PersistsTrimmedDescriptionAndRetainsItOnRedraft()
    {
        var createLineResponse = await _client.PostAsJsonAsync("/invoice-lines", new
        {
            invoiceId = TestData.RiversideInvoiceId,
            sortOrder = 1,
            type = InvoiceLineType.PerformanceFee,
            description = "Headline performance",
            quantity = 1m,
            unitPrice = 200m,
        }, TestContext.Current.CancellationToken);
        createLineResponse.EnsureSuccessStatusCode();

        var updateResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/description", new
        {
            description = "  June performance services  ",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedInvoice = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("June performance services", updatedInvoice.GetProperty("description").GetString());
        Assert.Equal(TestAuthContext.UserId, updatedInvoice.GetProperty("updatedByUserId").GetGuid());
        Assert.Single(updatedInvoice.GetProperty("lines").EnumerateArray());

        var redraftResponse = await _client.PostAsync($"/invoices/{TestData.RiversideInvoiceId}/redraft", null, TestContext.Current.CancellationToken);
        redraftResponse.EnsureSuccessStatusCode();

        var pdfResponse = await _client.GetAsync($"/invoices/{TestData.RiversideInvoiceId}/pdf", TestContext.Current.CancellationToken);
        pdfResponse.EnsureSuccessStatusCode();
        var pdfText = Encoding.ASCII.GetString(await pdfResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Contains("June performance services", pdfText);
    }

    [Fact]
    public async Task UpdateDescription_WhenBlankOrInvoiceIsNotDraft_ReturnsValidationProblem()
    {
        var blankResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/description", new
        {
            description = "   ",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, blankResponse.StatusCode);
        var blankProblem = await blankResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Invoice description is required.", blankProblem.GetProperty("errors").GetProperty("description")[0].GetString());

        var issuedResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/description", new
        {
            description = "Updated description",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, issuedResponse.StatusCode);
        var issuedProblem = await issuedResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Only Draft invoices can have their description changed.", issuedProblem.GetProperty("errors").GetProperty("status")[0].GetString());
    }

    [Fact]
    public async Task UpdateDescription_WhenInvoiceIsNotVisible_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());

        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/description", new
        {
            description = "Not visible",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddAdjustment_WhenRequestValid_AddsManualAdjustmentLineAndUpdatesInvoiceTotal()
    {
        var createLineResponse = await _client.PostAsJsonAsync("/invoice-lines", new
        {
            invoiceId = TestData.RiversideInvoiceId,
            sortOrder = 1,
            type = InvoiceLineType.PerformanceFee,
            description = "Headline performance",
            quantity = 1m,
            unitPrice = 200m,
        }, TestContext.Current.CancellationToken);
        createLineResponse.EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/adjustments", new
        {
            amount = -25m,
            reason = "Goodwill discount",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(175m, updatedInvoice.GetProperty("total").GetDecimal());

        var manualAdjustment = updatedInvoice.GetProperty("lines")
            .EnumerateArray()
            .Single(line => line.GetProperty("type").GetString() == "ManualAdjustment");

        Assert.Equal("Manual adjustment: Goodwill discount", manualAdjustment.GetProperty("description").GetString());
        Assert.Equal(-25m, manualAdjustment.GetProperty("lineTotal").GetDecimal());
        Assert.Equal(TestAuthContext.UserId, manualAdjustment.GetProperty("createdByUserId").GetGuid());
        Assert.Equal(JsonValueKind.String, manualAdjustment.GetProperty("createdUtc").ValueKind);
        Assert.Equal(
            JsonValueKind.String,
            manualAdjustment.GetProperty("calculationNotes").ValueKind);
        Assert.Equal("Current", updatedInvoice.GetProperty("documentState").GetString());
        Assert.Equal(
            updatedInvoice.GetProperty("documentRevision").GetInt32(),
            updatedInvoice.GetProperty("pdfDocumentRevision").GetInt32());

        var pdfResponse = await _client.GetAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/pdf",
            TestContext.Current.CancellationToken);
        pdfResponse.EnsureSuccessStatusCode();
        var pdfText = Encoding.ASCII.GetString(
            await pdfResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Contains("Manual adjustment: Goodwill discount", pdfText);
        Assert.Contains("GBP 175.00", pdfText);
    }

    [Fact]
    public async Task RegeneratePdf_WhenDocumentPreviouslyFailed_RestoresCurrentDocumentWithoutReissueAudit()
    {
        await SetDocumentStateAsync(InvoiceDocumentState.Failed, "PDF rendering failed.");

        var response = await _client.PostAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/regenerate-pdf",
            null,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        Assert.Equal("Current", updatedInvoice.GetProperty("documentState").GetString());
        Assert.Equal(0, updatedInvoice.GetProperty("reissueCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, updatedInvoice.GetProperty("lastReissuedUtc").ValueKind);
    }

    [Fact]
    public async Task RemoveAdjustment_RegeneratesPdfAndPreventsGenericLineDeletion()
    {
        var createLineResponse = await _client.PostAsJsonAsync("/invoice-lines", new
        {
            invoiceId = TestData.RiversideInvoiceId,
            sortOrder = 1,
            type = InvoiceLineType.PerformanceFee,
            description = "Headline performance",
            quantity = 1m,
            unitPrice = 200m,
        }, TestContext.Current.CancellationToken);
        createLineResponse.EnsureSuccessStatusCode();

        var addResponse = await _client.PostAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/adjustments", new
        {
            amount = -25m,
            reason = "Goodwill discount",
        }, TestContext.Current.CancellationToken);
        addResponse.EnsureSuccessStatusCode();
        var adjustedInvoice = await addResponse.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        var adjustmentId = adjustedInvoice.GetProperty("lines")
            .EnumerateArray()
            .Single(line => line.GetProperty("type").GetString() == "ManualAdjustment")
            .GetProperty("id")
            .GetGuid();

        var genericDeleteResponse = await _client.DeleteAsync(
            $"/invoice-lines/{adjustmentId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, genericDeleteResponse.StatusCode);

        var removalResponse = await _client.DeleteAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/adjustments/{adjustmentId}",
            TestContext.Current.CancellationToken);
        removalResponse.EnsureSuccessStatusCode();
        var updatedInvoice = await removalResponse.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        Assert.Equal(200m, updatedInvoice.GetProperty("total").GetDecimal());
        Assert.Equal("Current", updatedInvoice.GetProperty("documentState").GetString());
        Assert.DoesNotContain(
            updatedInvoice.GetProperty("lines").EnumerateArray(),
            line => line.GetProperty("type").GetString() == "ManualAdjustment");

        var pdfResponse = await _client.GetAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/pdf",
            TestContext.Current.CancellationToken);
        pdfResponse.EnsureSuccessStatusCode();
        var pdfText = Encoding.ASCII.GetString(
            await pdfResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain("Manual adjustment: Goodwill discount", pdfText);
        Assert.Contains("GBP 200.00", pdfText);
    }

    [Fact]
    public async Task AddAdjustment_WhenPdfRegenerationFails_PersistsAdjustmentAndBlocksDocumentAccess()
    {
        using var failingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInvoicePdfRenderer>();
                services.AddScoped<IInvoicePdfRenderer, ThrowingInvoicePdfRenderer>();
            }));
        using var client = failingFactory.CreateClient();

        var response = await client.PostAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/adjustments", new
        {
            amount = -25m,
            reason = "Goodwill discount",
        }, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        Assert.Equal(-25m, updatedInvoice.GetProperty("total").GetDecimal());
        Assert.Contains(
            updatedInvoice.GetProperty("lines").EnumerateArray(),
            line => line.GetProperty("type").GetString() == "ManualAdjustment");
        Assert.Equal("Failed", updatedInvoice.GetProperty("documentState").GetString());
        Assert.Contains(
            "adjustment was saved",
            updatedInvoice.GetProperty("documentFailureMessage").GetString(),
            StringComparison.OrdinalIgnoreCase);

        var pdfResponse = await client.GetAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/pdf",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, pdfResponse.StatusCode);
    }

    [Fact]
    public async Task RemoveAdjustment_WhenPdfRegenerationFails_PersistsRemovalAndBlocksDocumentAccess()
    {
        using var failingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInvoicePdfRenderer>();
                services.AddScoped<IInvoicePdfRenderer, ThrowingInvoicePdfRenderer>();
            }));
        using var client = failingFactory.CreateClient();
        var adjustmentId = Guid.NewGuid();
        using (var scope = failingFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.InvoiceLines.Add(new InvoiceLine
            {
                Id = adjustmentId,
                InvoiceId = TestData.RiversideInvoiceId,
                CreatedByUserId = TestAuthContext.UserId,
                CreatedUtc = DateTimeOffset.UtcNow,
                SortOrder = 1,
                Type = InvoiceLineType.ManualAdjustment,
                Description = "Manual adjustment: Goodwill discount",
                Quantity = 1,
                UnitPrice = -25m,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await client.DeleteAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/adjustments/{adjustmentId}",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        Assert.Equal(0m, updatedInvoice.GetProperty("total").GetDecimal());
        Assert.Equal("Failed", updatedInvoice.GetProperty("documentState").GetString());
        Assert.DoesNotContain(
            updatedInvoice.GetProperty("lines").EnumerateArray(),
            line => line.GetProperty("id").GetGuid() == adjustmentId);

        var pdfResponse = await client.GetAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/pdf",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, pdfResponse.StatusCode);
    }

    [Theory]
    [InlineData(InvoiceDocumentState.Missing)]
    [InlineData(InvoiceDocumentState.Regenerating)]
    [InlineData(InvoiceDocumentState.Failed)]
    public async Task UnavailableDocument_CannotBeDownloadedEmailedOrPublished(InvoiceDocumentState state)
    {
        await SetDocumentStateAsync(state, "PDF rendering failed.");

        var downloadResponse = await _client.GetAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/pdf",
            TestContext.Current.CancellationToken);
        var emailResponse = await _client.PostAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/send-email",
            null,
            TestContext.Current.CancellationToken);
        var publishResponse = await _client.PostAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/publish/google-drive",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, downloadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, publishResponse.StatusCode);
        var expectedMessage = state switch
        {
            InvoiceDocumentState.Missing => "Invoice PDF is missing.",
            InvoiceDocumentState.Regenerating => "Invoice PDF is regenerating.",
            _ => "PDF rendering failed.",
        };
        Assert.Contains(
            expectedMessage,
            await downloadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_factory.Emails.SentEmails);
    }

    [Fact]
    public async Task StaleDocument_CannotBeDownloadedEmailedOrPublished()
    {
        await SetDocumentStateAsync(InvoiceDocumentState.Current, null);

        var downloadResponse = await _client.GetAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/pdf",
            TestContext.Current.CancellationToken);
        var emailResponse = await _client.PostAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/send-email",
            null,
            TestContext.Current.CancellationToken);
        var publishResponse = await _client.PostAsync(
            $"/invoices/{TestData.RiversideInvoiceId}/publish/google-drive",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, downloadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, publishResponse.StatusCode);
        Assert.Empty(_factory.Emails.SentEmails);
    }

    private async Task SetDocumentStateAsync(InvoiceDocumentState state, string? failureMessage)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await db.Invoices.SingleAsync(
            value => value.Id == TestData.RiversideInvoiceId,
            TestContext.Current.CancellationToken);
        invoice.DocumentState = state;
        invoice.DocumentRevision = 2;
        invoice.PdfDocumentRevision = 1;
        invoice.DocumentFailureMessage = failureMessage;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class ThrowingInvoicePdfRenderer : IInvoicePdfRenderer
    {
        public byte[] RenderInvoicePdf(
            Invoice invoice,
            Client client,
            Gig? gig,
            IReadOnlyCollection<InvoiceLine> lines,
            SellerProfile? sellerProfile)
        {
            throw new InvalidOperationException("Renderer unavailable.");
        }
    }

    [Fact]
    public async Task AddAdjustment_WhenReasonMissing_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/adjustments", new
        {
            amount = 25m,
            reason = "   ",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Adjustment reason is required.", problem.GetProperty("errors").GetProperty("reason")[0].GetString());
    }

    [Fact]
    public async Task DeleteInvoice_WhenDraft_DeletesInvoice()
    {
        var createInvoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "INV-DELETE-DRAFT",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-06-01",
            dueDate = "2026-06-15",
            status = "Draft",
            description = "Draft invoice to delete",
        }, TestContext.Current.CancellationToken);
        createInvoiceResponse.EnsureSuccessStatusCode();

        var createdInvoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var invoiceId = createdInvoice.GetProperty("id").GetGuid();

        var deleteResponse = await _client.DeleteAsync($"/invoices/{invoiceId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/invoices/{invoiceId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteInvoice_WhenDraft_UnlinksAssociatedGigs()
    {
        var createInvoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "INV-DELETE-LINKED",
            clientId = TestData.FoxAndFinchId,
            invoiceDate = "2026-06-02",
            dueDate = "2026-06-16",
            status = "Draft",
            description = "Draft invoice linked to a gig",
        }, TestContext.Current.CancellationToken);
        createInvoiceResponse.EnsureSuccessStatusCode();

        var createdInvoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var invoiceId = createdInvoice.GetProperty("id").GetGuid();

        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId,
            title = "Invoice deletion unlink test",
            date = "2026-06-10",
            venue = "Town Hall",
            fee = 250.00m,
            travelMiles = 0m,
            passengerCount = (int?)null,
            notes = "Should be unlinked when invoice is deleted",
            wasDriving = false,
            status = "Completed",
            expenses = Array.Empty<object>(),
            invoicedAt = (string?)null,
        }, TestContext.Current.CancellationToken);
        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var gigId = createdGig.GetProperty("id").GetGuid();
        Assert.Equal(invoiceId, createdGig.GetProperty("invoiceId").GetGuid());
        Assert.True(createdGig.GetProperty("isInvoiced").GetBoolean());
        Assert.Equal(JsonValueKind.String, createdGig.GetProperty("invoicedAt").ValueKind);

        var deleteResponse = await _client.DeleteAsync($"/invoices/{invoiceId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var gigResponse = await _client.GetAsync($"/gigs/{gigId}", TestContext.Current.CancellationToken);
        gigResponse.EnsureSuccessStatusCode();

        var gig = await gigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Null, gig.GetProperty("invoiceId").ValueKind);
        Assert.False(gig.GetProperty("isInvoiced").GetBoolean());
        Assert.Equal(JsonValueKind.Null, gig.GetProperty("invoicedAt").ValueKind);
    }

    [Fact]
    public async Task DeleteInvoice_WhenNotDraft_ReturnsValidationProblem()
    {
        var response = await _client.DeleteAsync($"/invoices/{TestData.FoxInvoiceId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Only Draft invoices can be deleted. Issued invoices must be retained for reporting.",
            problem.GetProperty("errors").GetProperty("status")[0].GetString());
    }

    [Fact]
    public async Task GetInvoices_WhenSignedInAsDifferentUser_ReturnsOnlyVisibleInvoices()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());

        var response = await _client.GetAsync("/invoices", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var invoices = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Array, invoices.ValueKind);
        Assert.Empty(invoices.EnumerateArray());
    }
}
