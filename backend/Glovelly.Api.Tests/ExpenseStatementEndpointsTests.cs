using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class ExpenseStatementEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public ExpenseStatementEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preview_GroupsExpensesByGigCalculatesTotalsAndIncludesReceiptMetadata()
    {
        var firstGig = await CreateGigAsync(
            "Statement dinner",
            "2026-07-01",
            "Hotel Bar",
            [
                new("Parking", 12.50m),
                new("Dinner", 24.75m),
            ]);
        var secondGig = await CreateGigAsync(
            "Statement train",
            "2026-07-02",
            "Concert Hall",
            [
                new("Train", 44.10m),
            ]);
        await UploadReceiptAsync(firstGig.GigId, firstGig.ExpenseIds[0], "parking.pdf", "parking receipt");

        var response = await _client.PostAsJsonAsync("/expense-statements/preview", new
        {
            clientId = TestData.FoxAndFinchId,
            gigIds = new[] { secondGig.GigId, firstGig.GigId },
            includeReceiptAttachments = true,
            includeReceiptAppendix = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var statement = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(TestData.FoxAndFinchId, statement.GetProperty("clientId").GetGuid());
        Assert.Equal("Fox & Finch Events", statement.GetProperty("clientName").GetString());
        Assert.Equal(81.35m, statement.GetProperty("total").GetDecimal());
        Assert.Equal(3, statement.GetProperty("expenseCount").GetInt32());
        Assert.Equal(1, statement.GetProperty("receiptAttachmentCount").GetInt32());

        var gigs = statement.GetProperty("gigs").EnumerateArray().ToArray();
        Assert.Equal(2, gigs.Length);
        Assert.Equal(firstGig.GigId, gigs[0].GetProperty("gigId").GetGuid());
        Assert.Equal(37.25m, gigs[0].GetProperty("total").GetDecimal());
        Assert.Equal(secondGig.GigId, gigs[1].GetProperty("gigId").GetGuid());
        Assert.Equal(44.10m, gigs[1].GetProperty("total").GetDecimal());

        var firstExpense = gigs[0].GetProperty("expenses")[0];
        var attachments = firstExpense.GetProperty("attachments").EnumerateArray().ToArray();
        Assert.Single(attachments);
        Assert.Equal("parking.pdf", attachments[0].GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", attachments[0].GetProperty("contentType").GetString());
        Assert.False(attachments[0].TryGetProperty("storageKey", out _));
    }

    [Fact]
    public async Task Preview_ExcludesReimbursedExpensesByDefault()
    {
        var gig = await CreateGigAsync(
            "Reimbursed expenses",
            "2026-07-03",
            "Town Hall",
            [new("Hotel", 120.00m)]);

        var updateResponse = await _client.PatchAsJsonAsync($"/gigs/{gig.GigId}/expenses/reimbursement", new
        {
            expenseIds = new[] { gig.ExpenseIds[0] },
            status = "Reimbursed",
            reimbursedAt = "2026-07-10T00:00:00Z",
            method = "Bank transfer",
            note = "Paid by client",
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedGig = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var updatedExpense = updatedGig.GetProperty("expenses")[0];
        Assert.Equal("Reimbursed", updatedExpense.GetProperty("reimbursementStatus").GetString());
        Assert.Equal("Bank transfer", updatedExpense.GetProperty("reimbursementMethod").GetString());

        var defaultResponse = await _client.PostAsJsonAsync("/expense-statements/preview", new
        {
            clientId = TestData.FoxAndFinchId,
            gigIds = new[] { gig.GigId },
            includeReceiptAttachments = true,
            includeReceiptAppendix = false,
        });

        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);

        var defaultStatement = await defaultResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Empty(defaultStatement.GetProperty("gigs").EnumerateArray());
        Assert.Equal(0m, defaultStatement.GetProperty("total").GetDecimal());

        var includedResponse = await _client.PostAsJsonAsync("/expense-statements/preview", new
        {
            clientId = TestData.FoxAndFinchId,
            gigIds = new[] { gig.GigId },
            includeReceiptAttachments = true,
            includeReceiptAppendix = false,
            includeReimbursedExpenses = true,
        });

        Assert.Equal(HttpStatusCode.OK, includedResponse.StatusCode);

        var includedStatement = await includedResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Single(includedStatement.GetProperty("gigs").EnumerateArray());
        Assert.Equal(120.00m, includedStatement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Preview_AllowsInvoicedGigsForExpenseStatements()
    {
        var gig = await CreateGigAsync(
            "Invoiced statement expenses",
            "2026-07-08",
            "Arts Centre",
            [new("Parking", 9.50m)],
            TestData.FoxInvoiceId);

        var response = await _client.PostAsJsonAsync("/expense-statements/preview", new
        {
            clientId = TestData.FoxAndFinchId,
            gigIds = new[] { gig.GigId },
            includeReceiptAttachments = true,
            includeReceiptAppendix = false,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var statement = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var statementGig = Assert.Single(statement.GetProperty("gigs").EnumerateArray());
        Assert.Equal(gig.GigId, statementGig.GetProperty("gigId").GetGuid());
        Assert.True(statementGig.GetProperty("isInvoiced").GetBoolean());
        Assert.Equal(9.50m, statement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task GenerateInvoice_ExcludesReimbursedExpensesByDefault()
    {
        var gig = await CreateGigAsync(
            "Invoice reimbursement filter",
            "2026-07-07",
            "Club",
            [
                new("Parking", 10.00m),
                new("Hotel", 120.00m),
            ]);

        var reimbursementResponse = await _client.PatchAsJsonAsync($"/gigs/{gig.GigId}/expenses/reimbursement", new
        {
            expenseIds = new[] { gig.ExpenseIds[1] },
            status = "Reimbursed",
            reimbursedAt = "2026-07-11T00:00:00Z",
            method = "Cash",
            note = "Settled on the night",
        });
        reimbursementResponse.EnsureSuccessStatusCode();

        var invoiceResponse = await _client.PostAsync($"/gigs/{gig.GigId}/generate-invoice", null);
        Assert.Equal(HttpStatusCode.Created, invoiceResponse.StatusCode);

        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lines = invoice.GetProperty("lines").EnumerateArray().ToArray();

        Assert.Contains(lines, line => line.GetProperty("description").GetString() == "Parking");
        Assert.DoesNotContain(lines, line => line.GetProperty("description").GetString() == "Hotel");
    }

    [Fact]
    public async Task Pdf_ReturnsDownloadablePdfWithoutMutatingGigInvoiceState()
    {
        var gig = await CreateGigAsync(
            "PDF expenses",
            "2026-07-04",
            "Studio",
            [new("Taxi", 18.90m)]);
        await UploadReceiptAsync(gig.GigId, gig.ExpenseIds[0], "taxi.pdf", "taxi receipt");

        var response = await _client.PostAsJsonAsync("/expense-statements/pdf", new
        {
            clientId = TestData.FoxAndFinchId,
            gigIds = new[] { gig.GigId },
            includeReceiptAttachments = true,
            includeReceiptAppendix = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Expense-Statement-Fox-Finch-Events-", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);

        var pdfText = Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.StartsWith("%PDF-1.4", pdfText);
        Assert.Contains("Expense Statement", pdfText);
        Assert.Contains("PDF expenses", pdfText);
        Assert.Contains("Taxi", pdfText);
        Assert.Contains("Receipt Appendix", pdfText);
        Assert.Contains("taxi.pdf", pdfText);

        var gigResponse = await _client.GetAsync($"/gigs/{gig.GigId}");
        gigResponse.EnsureSuccessStatusCode();
        var currentGig = await gigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Null, currentGig.GetProperty("invoiceId").ValueKind);
        Assert.Equal(JsonValueKind.Null, currentGig.GetProperty("invoicedAt").ValueKind);
    }

    [Fact]
    public async Task Preview_WithGigFromDifferentClient_ReturnsValidationProblem()
    {
        var foxGig = await CreateGigAsync("Fox gig", "2026-07-05", "Club", [new("Taxi", 10.00m)]);
        var riversideResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.RiversideId,
            title = "Riverside gig",
            date = "2026-07-06",
            venue = "Riverside",
            fee = 100m,
            travelMiles = 0m,
            wasDriving = false,
            status = 1,
            expenses = new[] { new { description = "Parking", amount = 8.00m } },
            invoicedAt = (string?)null,
        });
        riversideResponse.EnsureSuccessStatusCode();
        var riversideGig = await riversideResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await _client.PostAsJsonAsync("/expense-statements/preview", new
        {
            clientId = TestData.FoxAndFinchId,
            gigIds = new[] { foxGig.GigId, riversideGig.GetProperty("id").GetGuid() },
            includeReceiptAttachments = false,
            includeReceiptAppendix = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "All selected gigs must belong to the selected client.",
            problem.GetProperty("errors").GetProperty("gigIds")[0].GetString());
    }

    private async Task<(Guid GigId, Guid[] ExpenseIds)> CreateGigAsync(
        string title,
        string date,
        string venue,
        IReadOnlyList<ExpenseSeed> expenses,
        Guid? invoiceId = null)
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId,
            title,
            date,
            venue,
            fee = 100m,
            travelMiles = 0m,
            wasDriving = false,
            status = 1,
            expenses = expenses.Select(expense => new
            {
                description = expense.Description,
                amount = expense.Amount,
            }),
            invoicedAt = (string?)null,
        });

        response.EnsureSuccessStatusCode();

        var gig = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return (
            gig.GetProperty("id").GetGuid(),
            gig.GetProperty("expenses")
                .EnumerateArray()
                .Select(expense => expense.GetProperty("id").GetGuid())
                .ToArray());
    }

    private async Task UploadReceiptAsync(Guid gigId, Guid expenseId, string fileName, string content)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);

        var response = await _client.PostAsync($"/gigs/{gigId}/expenses/{expenseId}/attachments", form);
        response.EnsureSuccessStatusCode();
    }

    private sealed record ExpenseSeed(string Description, decimal Amount);
}
