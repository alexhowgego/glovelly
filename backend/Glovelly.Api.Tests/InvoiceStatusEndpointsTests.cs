using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class InvoiceStatusEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public InvoiceStatusEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateStatus_WhenTransitionAllowed_PersistsStatusAndAuditFields()
    {
        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Paid",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Paid", updatedInvoice.GetProperty("status").GetString());
        Assert.Equal(TestAuthContext.UserId, updatedInvoice.GetProperty("updatedByUserId").GetGuid());
        Assert.Equal(JsonValueKind.String, updatedInvoice.GetProperty("statusUpdatedUtc").ValueKind);
    }

    [Fact]
    public async Task UpdateStatus_WhenTransitionNotAllowed_ReturnsValidationProblem()
    {
        var makePaidResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Paid",
        });
        makePaidResponse.EnsureSuccessStatusCode();

        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Cancelled",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Invoice status cannot move from Paid to Cancelled.",
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
        });
        createLineResponse.EnsureSuccessStatusCode();

        var response = await _client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new
        {
            status = "Paid",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(300m, updatedInvoice.GetProperty("total").GetDecimal());
        Assert.Single(updatedInvoice.GetProperty("lines").EnumerateArray());
    }

    [Fact]
    public async Task Reissue_WhenInvoiceExists_RegeneratesPdfAndLogsActionWithoutChangingFinancials()
    {
        var createLineResponse = await _client.PostAsJsonAsync("/invoice-lines", new
        {
            invoiceId = TestData.FoxInvoiceId,
            sortOrder = 1,
            type = InvoiceLineType.PerformanceFee,
            description = "Headline performance",
            quantity = 2m,
            unitPrice = 150m,
        });
        createLineResponse.EnsureSuccessStatusCode();

        var response = await _client.PostAsync($"/invoices/{TestData.FoxInvoiceId}/reissue", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedInvoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(300m, updatedInvoice.GetProperty("total").GetDecimal());
        Assert.Equal(1, updatedInvoice.GetProperty("reissueCount").GetInt32());
        Assert.Equal(TestAuthContext.UserId, updatedInvoice.GetProperty("lastReissuedByUserId").GetGuid());
        Assert.Equal(JsonValueKind.String, updatedInvoice.GetProperty("lastReissuedUtc").ValueKind);
        Assert.Equal(JsonValueKind.String, updatedInvoice.GetProperty("pdfBlob").ValueKind);
        Assert.Single(updatedInvoice.GetProperty("lines").EnumerateArray());
    }
}
