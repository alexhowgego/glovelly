using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GigEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public GigEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateGig_WithoutInvoice_ReturnsDerivedNotInvoiced()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Uninvoiced rehearsal",
            date = "2026-06-01",
            venue = "Band Room",
            fee = 120.00m,
            travelMiles = 8.50m,
            notes = "Evening prep",
            wasDriving = true,
            status = 1,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var gig = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, gig.ValueKind);
        Assert.Equal(JsonValueKind.Null, gig.GetProperty("invoiceId").ValueKind);
        Assert.False(gig.GetProperty("isInvoiced").GetBoolean());
        Assert.Equal(JsonValueKind.Null, gig.GetProperty("invoicedAt").ValueKind);
    }

    [Fact]
    public async Task CreateGig_WithInvoice_ReturnsDerivedInvoicedAndAuditTimestamp()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Invoiced performance",
            date = "2026-06-02",
            venue = "Town Hall",
            fee = 300.00m,
            travelMiles = 14.00m,
            notes = "Festival set",
            wasDriving = false,
            status = 2,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var gig = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, gig.ValueKind);
        Assert.Equal(TestData.FoxInvoiceId, gig.GetProperty("invoiceId").GetGuid());
        Assert.True(gig.GetProperty("isInvoiced").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(gig.GetProperty("invoicedAt").GetString()));
    }

    [Fact]
    public async Task CreateGig_WithMissingInvoice_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            title = "Bad invoice link",
            date = "2026-06-03",
            venue = "Club",
            fee = 150.00m,
            travelMiles = 10.00m,
            notes = "Test case",
            wasDriving = true,
            status = 0,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, problem.ValueKind);
        Assert.Equal("Invoice does not exist.", problem.GetProperty("errors").GetProperty("invoiceId")[0].GetString());
    }

    [Fact]
    public async Task CreateGig_WithInvoiceForDifferentClient_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.RiversideInvoiceId,
            title = "Cross-client invoice link",
            date = "2026-06-04",
            venue = "Theatre",
            fee = 180.00m,
            travelMiles = 6.00m,
            notes = "Should fail",
            wasDriving = false,
            status = 0,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, problem.ValueKind);
        Assert.Equal("Invoice client must match the gig client.", problem.GetProperty("errors").GetProperty("invoiceId")[0].GetString());
    }

    [Fact]
    public async Task UpdateGig_WithoutInvoiceInRequest_PreservesExistingInvoiceLink()
    {
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Linked gig",
            date = "2026-06-05",
            venue = "Warehouse",
            fee = 225.00m,
            travelMiles = 12.00m,
            notes = "Created for update test",
            wasDriving = true,
            status = 1,
            invoicedAt = (string?)null,
        });

        createResponse.EnsureSuccessStatusCode();

        var createdGig = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Object, createdGig.ValueKind);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var updateResponse = await _client.PutAsJsonAsync($"/gigs/{gigId}", new
        {
            id = gigId,
            clientId = TestData.FoxAndFinchId,
            invoiceId = (Guid?)null,
            title = "Linked gig",
            date = "2026-06-05",
            venue = "Warehouse",
            fee = 225.00m,
            travelMiles = 12.00m,
            notes = "Invoice removed",
            wasDriving = true,
            status = 1,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedGig = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, updatedGig.ValueKind);
        Assert.Equal(TestData.FoxInvoiceId, updatedGig.GetProperty("invoiceId").GetGuid());
        Assert.True(updatedGig.GetProperty("isInvoiced").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, updatedGig.GetProperty("invoicedAt").ValueKind);
    }

    [Fact]
    public async Task UpdateGig_WithExpenses_ReplacesExpenseCollectionAndReturnsUpdatedGig()
    {
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Expense refresh test",
            date = "2026-06-08",
            venue = "Assembly Rooms",
            fee = 180.00m,
            travelMiles = 0.00m,
            notes = "Initial expense set",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Parking",
                    amount = 12.00m,
                },
            },
            invoicedAt = (string?)null,
        });

        createResponse.EnsureSuccessStatusCode();

        var createdGig = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var updateResponse = await _client.PutAsJsonAsync($"/gigs/{gigId}", new
        {
            id = gigId,
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Expense refresh test",
            date = "2026-06-08",
            venue = "Assembly Rooms",
            fee = 180.00m,
            travelMiles = 0.00m,
            passengerCount = (int?)null,
            notes = "Updated expense set",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Tolls",
                    amount = 8.50m,
                },
                new
                {
                    description = "Parking",
                    amount = 14.00m,
                },
            },
            invoicedAt = createdGig.GetProperty("invoicedAt").GetString(),
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedGig = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var updatedExpenses = updatedGig.GetProperty("expenses").EnumerateArray().OrderBy(expense => expense.GetProperty("sortOrder").GetInt32()).ToArray();

        Assert.Equal(2, updatedExpenses.Length);
        Assert.Equal("Tolls", updatedExpenses[0].GetProperty("description").GetString());
        Assert.Equal(8.50m, updatedExpenses[0].GetProperty("amount").GetDecimal());
        Assert.Equal("Parking", updatedExpenses[1].GetProperty("description").GetString());
        Assert.Equal(14.00m, updatedExpenses[1].GetProperty("amount").GetDecimal());

        var invoiceResponse = await _client.GetAsync($"/invoices/{TestData.FoxInvoiceId}");
        invoiceResponse.EnsureSuccessStatusCode();

        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lines = invoice.GetProperty("lines").EnumerateArray().Where(line => line.GetProperty("gigId").GetGuid() == gigId).ToArray();

        Assert.Equal(2, lines.Length);
        Assert.Contains(lines, line => line.GetProperty("description").GetString() == "Performance fee for Expense refresh test (2026-06-08)");
        Assert.Contains(lines, line =>
            line.GetProperty("description").GetString() == "Parking" &&
            line.GetProperty("unitPrice").GetDecimal() == 12.00m);
        Assert.DoesNotContain(lines, line => line.GetProperty("description").GetString() == "Tolls");
    }

    [Fact]
    public async Task ExpenseAttachmentFlow_UploadsListsDownloadsAndDeletesReceipt()
    {
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Receipt test",
            date = "2026-06-09",
            venue = "Station",
            fee = 120.00m,
            travelMiles = 0.00m,
            notes = "Receipt test",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Taxi",
                    amount = 38.42m,
                },
            },
            invoicedAt = (string?)null,
        });

        createResponse.EnsureSuccessStatusCode();

        var createdGig = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = createdGig.GetProperty("id").GetGuid();
        var expenseId = createdGig.GetProperty("expenses")[0].GetProperty("id").GetGuid();

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent("receipt evidence"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "uber-receipt.pdf");

        var uploadResponse = await _client.PostAsync($"/gigs/{gigId}/expenses/{expenseId}/attachments", form);

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var attachment = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var attachmentId = attachment.GetProperty("id").GetGuid();
        Assert.Equal("uber-receipt.pdf", attachment.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", attachment.GetProperty("contentType").GetString());
        Assert.Equal("receipt evidence"u8.Length, attachment.GetProperty("sizeBytes").GetInt64());
        Assert.False(attachment.TryGetProperty("storageKey", out _));

        var gigResponse = await _client.GetAsync($"/gigs/{gigId}");
        gigResponse.EnsureSuccessStatusCode();

        var gig = await gigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var attachments = gig.GetProperty("expenses")[0].GetProperty("attachments").EnumerateArray().ToArray();
        Assert.Single(attachments);
        Assert.Equal(attachmentId, attachments[0].GetProperty("id").GetGuid());

        var downloadResponse = await _client.GetAsync($"/gigs/{gigId}/expenses/{expenseId}/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("receipt evidence", await downloadResponse.Content.ReadAsStringAsync());

        var deleteResponse = await _client.DeleteAsync($"/gigs/{gigId}/expenses/{expenseId}/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var missingDownloadResponse = await _client.GetAsync($"/gigs/{gigId}/expenses/{expenseId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.NotFound, missingDownloadResponse.StatusCode);
    }

    [Fact]
    public async Task UploadExpenseAttachment_WithUnsupportedType_ReturnsValidationProblem()
    {
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Receipt validation test",
            date = "2026-06-10",
            venue = "Station",
            fee = 120.00m,
            travelMiles = 0.00m,
            notes = "Receipt test",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Taxi",
                    amount = 38.42m,
                },
            },
            invoicedAt = (string?)null,
        });

        createResponse.EnsureSuccessStatusCode();

        var createdGig = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = createdGig.GetProperty("id").GetGuid();
        var expenseId = createdGig.GetProperty("expenses")[0].GetProperty("id").GetGuid();

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent("bad"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "receipt.txt");

        var response = await _client.PostAsync($"/gigs/{gigId}/expenses/{expenseId}/attachments", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Receipt files must be PDF, JPG, PNG, WebP or HEIC.", problem.GetProperty("errors").GetProperty("file")[0].GetString());
    }

    [Fact]
    public async Task QuickReceiptDraft_WithNearbyGig_CreatesDraftExpenseAndAttachment()
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Yesterday receipt match",
            date = today.AddDays(-1).ToString("yyyy-MM-dd"),
            venue = "Station",
            fee = 120.00m,
            travelMiles = 0.00m,
            notes = "Receipt draft test",
            wasDriving = true,
            status = 1,
            expenses = Array.Empty<object>(),
            invoicedAt = (string?)null,
        });

        createResponse.EnsureSuccessStatusCode();

        using var form = BuildReceiptDraftForm("taxi receipt"u8.ToArray(), "taxi.jpg", "image/jpeg");

        var response = await _client.PostAsync("/gigs/receipt-drafts", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.GetProperty("inferredGig").GetBoolean());

        var gig = result.GetProperty("gig");
        var expense = Assert.Single(gig.GetProperty("expenses").EnumerateArray());
        Assert.Equal("Receipt draft", expense.GetProperty("description").GetString());
        Assert.Equal(0m, expense.GetProperty("amount").GetDecimal());

        var attachment = Assert.Single(expense.GetProperty("attachments").EnumerateArray());
        Assert.Equal("taxi.jpg", attachment.GetProperty("fileName").GetString());
        Assert.Equal("image/jpeg", attachment.GetProperty("contentType").GetString());
        Assert.Equal("taxi receipt"u8.Length, attachment.GetProperty("sizeBytes").GetInt64());

        var candidates = result.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Single(candidates);
        Assert.Equal(1, candidates[0].GetProperty("daysFromToday").GetInt32());
        Assert.True(candidates[0].GetProperty("isSelected").GetBoolean());
    }

    [Fact]
    public async Task QuickReceiptDraft_WithCandidateInsideAmbiguityWindow_FlagsNearbyCandidates()
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        foreach (var (title, offset) in new[] { ("Yesterday show", -1), ("Tomorrow show", 1), ("Next week show", 7) })
        {
            var createResponse = await _client.PostAsJsonAsync("/gigs", new
            {
                clientId = TestData.FoxAndFinchId,
                title,
                date = today.AddDays(offset).ToString("yyyy-MM-dd"),
                venue = "Theatre",
                fee = 120.00m,
                travelMiles = 0.00m,
                notes = "Ambiguous receipt draft test",
                wasDriving = true,
                status = 1,
                expenses = Array.Empty<object>(),
                invoicedAt = (string?)null,
            });

            createResponse.EnsureSuccessStatusCode();
        }

        using var form = BuildReceiptDraftForm("receipt"u8.ToArray(), "receipt.pdf", "application/pdf");

        var response = await _client.PostAsync("/gigs/receipt-drafts", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.GetProperty("hasNearbyCandidates").GetBoolean());

        var gig = result.GetProperty("gig");
        Assert.Equal("Yesterday show", gig.GetProperty("title").GetString());

        var candidates = result.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(3, candidates.Length);
        Assert.Equal("Yesterday show", candidates[0].GetProperty("title").GetString());
        Assert.True(candidates[0].GetProperty("isSelected").GetBoolean());
    }

    [Fact]
    public async Task QuickReceiptDraft_WithCandidateOutsideAmbiguityWindow_DoesNotFlagNearbyCandidates()
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        foreach (var (title, offset) in new[] { ("Today show", 0), ("Last month show", -20) })
        {
            var createResponse = await _client.PostAsJsonAsync("/gigs", new
            {
                clientId = TestData.FoxAndFinchId,
                title,
                date = today.AddDays(offset).ToString("yyyy-MM-dd"),
                venue = "Theatre",
                fee = 120.00m,
                travelMiles = 0.00m,
                notes = "Distant ambiguity test",
                wasDriving = true,
                status = 1,
                expenses = Array.Empty<object>(),
                invoicedAt = (string?)null,
            });

            createResponse.EnsureSuccessStatusCode();
        }

        using var form = BuildReceiptDraftForm("receipt"u8.ToArray(), "receipt.pdf", "application/pdf");

        var response = await _client.PostAsync("/gigs/receipt-drafts", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.False(result.GetProperty("hasNearbyCandidates").GetBoolean());

        var candidates = result.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(2, candidates.Length);
    }

    [Fact]
    public async Task QuickReceiptDraft_WithNoCandidateInsideWindow_ReturnsEmptyCandidates()
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        foreach (var (title, offset) in new[] { ("Old show", -45), ("Future show", 60) })
        {
            var createResponse = await _client.PostAsJsonAsync("/gigs", new
            {
                clientId = TestData.FoxAndFinchId,
                title,
                date = today.AddDays(offset).ToString("yyyy-MM-dd"),
                venue = "Theatre",
                fee = 120.00m,
                travelMiles = 0.00m,
                notes = "Distant receipt draft test",
                wasDriving = true,
                status = 1,
                expenses = Array.Empty<object>(),
                invoicedAt = (string?)null,
            });

            createResponse.EnsureSuccessStatusCode();
        }

        using var form = BuildReceiptDraftForm("receipt"u8.ToArray(), "receipt.pdf", "application/pdf");

        var response = await _client.PostAsync("/gigs/receipt-drafts", form);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("No gig was within 30 days. Choose a gig before saving this receipt draft.", result.GetProperty("message").GetString());
        Assert.Empty(result.GetProperty("candidates").EnumerateArray());
        Assert.Equal(30, result.GetProperty("autoAttachWindowDays").GetInt32());
    }

    [Fact]
    public async Task QuickReceiptDraft_WithExplicitGig_CreatesDraftWhenNearestIsOutsideWindow()
    {
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Future receipt target",
            date = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime).AddDays(60).ToString("yyyy-MM-dd"),
            venue = "Airport",
            fee = 120.00m,
            travelMiles = 0.00m,
            notes = "Explicit receipt draft test",
            wasDriving = true,
            status = 1,
            expenses = Array.Empty<object>(),
            invoicedAt = (string?)null,
        });

        createResponse.EnsureSuccessStatusCode();
        var createdGig = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = createdGig.GetProperty("id").GetGuid();

        using var form = BuildReceiptDraftForm("receipt"u8.ToArray(), "receipt.pdf", "application/pdf", gigId);

        var response = await _client.PostAsync("/gigs/receipt-drafts", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.False(result.GetProperty("inferredGig").GetBoolean());
        Assert.Equal(gigId, result.GetProperty("gig").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task UpdateQuickReceiptDraft_SavesDetailsAndMovesGig()
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        var firstGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Receipt first target",
            date = today.ToString("yyyy-MM-dd"),
            venue = "Station",
            fee = 120.00m,
            travelMiles = 0.00m,
            notes = "Receipt update test",
            wasDriving = true,
            status = 1,
            expenses = Array.Empty<object>(),
            invoicedAt = (string?)null,
        });
        firstGigResponse.EnsureSuccessStatusCode();

        var secondGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Receipt corrected target",
            date = today.AddDays(2).ToString("yyyy-MM-dd"),
            venue = "Hotel",
            fee = 120.00m,
            travelMiles = 0.00m,
            notes = "Receipt update test",
            wasDriving = true,
            status = 1,
            expenses = Array.Empty<object>(),
            invoicedAt = (string?)null,
        });
        secondGigResponse.EnsureSuccessStatusCode();

        var secondGig = await secondGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var secondGigId = secondGig.GetProperty("id").GetGuid();

        using var form = BuildReceiptDraftForm("receipt"u8.ToArray(), "receipt.pdf", "application/pdf");
        var quickResponse = await _client.PostAsync("/gigs/receipt-drafts", form);
        quickResponse.EnsureSuccessStatusCode();

        var quickDraft = await quickResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var expenseId = quickDraft.GetProperty("expenseId").GetGuid();

        var updateResponse = await _client.PatchAsJsonAsync($"/gigs/receipt-drafts/{expenseId}", new
        {
            gigId = secondGigId,
            description = "Taxi from station",
            amount = 18.75m,
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var result = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.GetProperty("moved").GetBoolean());

        var targetGig = result.GetProperty("gig");
        Assert.Equal(secondGigId, targetGig.GetProperty("id").GetGuid());
        var expense = Assert.Single(targetGig.GetProperty("expenses").EnumerateArray());
        Assert.Equal("Taxi from station", expense.GetProperty("description").GetString());
        Assert.Equal(18.75m, expense.GetProperty("amount").GetDecimal());
        Assert.Single(expense.GetProperty("attachments").EnumerateArray());

        var previousGig = result.GetProperty("previousGig");
        Assert.Empty(previousGig.GetProperty("expenses").EnumerateArray());
    }

    [Fact]
    public async Task CreateGig_WithInvoice_GeneratesMileagePassengerAndExpenseLines()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Mileage-rich booking",
            date = "2026-06-12",
            venue = "Civic Hall",
            fee = 300.00m,
            travelMiles = 10.50m,
            passengerCount = 2,
            notes = "Includes two guests and parking",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Parking",
                    amount = 12.00m,
                },
                new
                {
                    description = "Fuel",
                    amount = 20.00m,
                },
            },
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var gig = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = gig.GetProperty("id").GetGuid();

        var invoiceResponse = await _client.GetAsync($"/invoices/{TestData.FoxInvoiceId}");
        invoiceResponse.EnsureSuccessStatusCode();

        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lines = invoice.GetProperty("lines").EnumerateArray().OrderBy(line => line.GetProperty("sortOrder").GetInt32()).ToArray();

        Assert.Equal(5, lines.Length);
        Assert.All(lines, line => Assert.True(line.GetProperty("isSystemGenerated").GetBoolean()));
        Assert.All(lines, line => Assert.Equal(gigId, line.GetProperty("gigId").GetGuid()));

        Assert.Equal("PerformanceFee", lines[0].GetProperty("type").GetString());
        Assert.Equal(300.00m, lines[0].GetProperty("lineTotal").GetDecimal());

        Assert.Equal("Mileage", lines[1].GetProperty("type").GetString());
        Assert.Equal(10.50m, lines[1].GetProperty("quantity").GetDecimal());
        Assert.Equal(0.52m, lines[1].GetProperty("unitPrice").GetDecimal());
        Assert.Equal(5.46m, lines[1].GetProperty("lineTotal").GetDecimal());

        Assert.Equal("PassengerMileage", lines[2].GetProperty("type").GetString());
        Assert.Equal(21.00m, lines[2].GetProperty("quantity").GetDecimal());
        Assert.Equal(0.15m, lines[2].GetProperty("unitPrice").GetDecimal());
        Assert.Equal(3.15m, lines[2].GetProperty("lineTotal").GetDecimal());

        Assert.Equal("Parking", lines[3].GetProperty("description").GetString());
        Assert.Equal(12.00m, lines[3].GetProperty("lineTotal").GetDecimal());
        Assert.Equal("Fuel", lines[4].GetProperty("description").GetString());
        Assert.Equal(20.00m, lines[4].GetProperty("lineTotal").GetDecimal());

        Assert.Equal(340.61m, invoice.GetProperty("total").GetDecimal());
    }

    private static MultipartFormDataContent BuildReceiptDraftForm(
        byte[] content,
        string fileName,
        string contentType,
        Guid? gigId = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        if (gigId.HasValue)
        {
            form.Add(new StringContent(gigId.Value.ToString()), "gigId");
        }

        return form;
    }

    [Fact]
    public async Task CreateGig_UsesUserPricingFallbackWhenClientRatesAreMissing()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.RiversideId,
            invoiceId = TestData.RiversideInvoiceId,
            title = "Residency workshop",
            date = "2026-06-13",
            venue = "River Room",
            fee = 200.00m,
            travelMiles = 8.00m,
            passengerCount = 1,
            notes = "Uses user fallback rates",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Parking",
                    amount = 9.00m,
                },
            },
            invoicedAt = (string?)null,
        });

        response.EnsureSuccessStatusCode();

        var invoiceResponse = await _client.GetAsync($"/invoices/{TestData.RiversideInvoiceId}");
        invoiceResponse.EnsureSuccessStatusCode();

        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lines = invoice.GetProperty("lines").EnumerateArray().OrderBy(line => line.GetProperty("sortOrder").GetInt32()).ToArray();

        Assert.Equal(4, lines.Length);
        Assert.All(lines, line => Assert.True(line.GetProperty("isSystemGenerated").GetBoolean()));

        Assert.Equal(200.00m, lines[0].GetProperty("lineTotal").GetDecimal());
        Assert.Equal(3.60m, lines[1].GetProperty("lineTotal").GetDecimal());
        Assert.Equal(0.80m, lines[2].GetProperty("lineTotal").GetDecimal());
        Assert.Equal(9.00m, lines[3].GetProperty("lineTotal").GetDecimal());
        Assert.Equal(213.40m, invoice.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task CreateGig_WithMissingRequiredFields_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = Guid.Empty,
            title = "   ",
            date = "0001-01-01",
            venue = "",
            fee = -25.00m,
            travelMiles = -3.00m,
            wasDriving = true,
            status = 999,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var errors = problem.GetProperty("errors");

        Assert.Equal("Client is required.", errors.GetProperty("clientId")[0].GetString());
        Assert.Equal("Title is required.", errors.GetProperty("title")[0].GetString());
        Assert.Equal("Date is required.", errors.GetProperty("date")[0].GetString());
        Assert.Equal("Location or venue is required.", errors.GetProperty("venue")[0].GetString());
        Assert.Equal("Fee cannot be negative.", errors.GetProperty("fee")[0].GetString());
        Assert.Equal("Travel miles cannot be negative.", errors.GetProperty("travelMiles")[0].GetString());
        Assert.Equal("Status is invalid.", errors.GetProperty("status")[0].GetString());
    }

    [Fact]
    public async Task CreateGig_PersistsAndAppearsInGigList()
    {
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Summer garden party",
            date = "2026-07-11",
            venue = "Botanical Gardens",
            fee = 425.00m,
            notes = "Outdoor afternoon set",
            wasDriving = true,
            status = 1,
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdGig = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var createdGigId = createdGig.GetProperty("id").GetGuid();

        var listResponse = await _client.GetAsync("/gigs");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var gigs = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var savedGig = gigs.EnumerateArray().FirstOrDefault(gig => gig.GetProperty("id").GetGuid() == createdGigId);

        Assert.Equal(JsonValueKind.Object, savedGig.ValueKind);
        Assert.Equal(TestData.FoxAndFinchId, savedGig.GetProperty("clientId").GetGuid());
        Assert.Equal("Summer garden party", savedGig.GetProperty("title").GetString());
        Assert.Equal("2026-07-11", savedGig.GetProperty("date").GetString());
        Assert.Equal("Botanical Gardens", savedGig.GetProperty("venue").GetString());
        Assert.Equal(425.00m, savedGig.GetProperty("fee").GetDecimal());
        Assert.Equal("Confirmed", savedGig.GetProperty("status").GetString());
    }
}
