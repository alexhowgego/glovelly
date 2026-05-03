using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GigInvoiceGenerationTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public GigInvoiceGenerationTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateInvoice_FromGig_CreatesInvoiceLinesPdfAndGigLink()
    {
        var sellerProfileResponse = await _client.PutAsJsonAsync("/seller-profile", new
        {
            sellerName = "Glovelly Music Ltd",
            addressLine1 = "1 Chapel Street",
            city = "Manchester",
            country = "United Kingdom",
            email = "accounts@glovelly.test",
            accountName = "Glovelly Music Ltd",
            sortCode = "12-34-56",
            accountNumber = "12345678",
            paymentReferenceNote = "Use the invoice number as your reference.",
        });
        sellerProfileResponse.EnsureSuccessStatusCode();

        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "One-off corporate booking",
            date = "2026-06-20",
            venue = "King's House",
            fee = 500.00m,
            travelMiles = 15.5m,
            passengerCount = 1,
            notes = "Invoice generation flow",
            wasDriving = true,
            status = 1,
            invoicedAt = (string?)null,
        });

        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var generateResponse = await _client.PostAsync($"/gigs/{gigId}/generate-invoice", content: null);

        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);

        var invoice = await generateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal("Draft", invoice.GetProperty("status").GetString());
        Assert.Equal(TestData.FoxAndFinchId, invoice.GetProperty("clientId").GetGuid());
        Assert.Equal("In respect of One-off corporate booking at King's House on 2026-06-20.", invoice.GetProperty("description").GetString());
        Assert.False(string.IsNullOrWhiteSpace(invoice.GetProperty("invoiceNumber").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(invoice.GetProperty("pdfBlob").GetString()));
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(today, invoice.GetProperty("invoiceDate").GetString());
        Assert.Equal(
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            invoice.GetProperty("dueDate").GetString());

        var initialPdfText = Encoding.ASCII.GetString(
            Convert.FromBase64String(invoice.GetProperty("pdfBlob").GetString()!));
        Assert.Contains("Glovelly Music Ltd", initialPdfText);
        Assert.Contains("Account number: 12345678", initialPdfText);
        Assert.Contains("Payment note: Use the invoice number as your reference.", initialPdfText);

        var lines = invoice.GetProperty("lines").EnumerateArray().OrderBy(line => line.GetProperty("sortOrder").GetInt32()).ToArray();
        Assert.Equal(3, lines.Length);

        Assert.Equal("PerformanceFee", lines[0].GetProperty("type").GetString());
        Assert.Equal(500.00m, lines[0].GetProperty("lineTotal").GetDecimal());
        Assert.Equal("Mileage", lines[1].GetProperty("type").GetString());
        Assert.Equal(15.5m, lines[1].GetProperty("quantity").GetDecimal());
        Assert.Equal("PassengerMileage", lines[2].GetProperty("type").GetString());
        Assert.Equal(15.5m, lines[2].GetProperty("quantity").GetDecimal());
        Assert.Equal(510.385m, invoice.GetProperty("total").GetDecimal());

        var refreshedGigResponse = await _client.GetAsync($"/gigs/{gigId}");
        refreshedGigResponse.EnsureSuccessStatusCode();

        var refreshedGig = await refreshedGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(invoice.GetProperty("id").GetGuid(), refreshedGig.GetProperty("invoiceId").GetGuid());
        Assert.True(refreshedGig.GetProperty("isInvoiced").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(refreshedGig.GetProperty("invoicedAt").GetString()));

        var pdfResponse = await _client.GetAsync($"/invoices/{invoice.GetProperty("id").GetGuid()}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            $"{invoice.GetProperty("invoiceNumber").GetString()}.pdf",
            pdfResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.True(pdfBytes.Length > 0);

        var pdfText = Encoding.ASCII.GetString(pdfBytes);
        Assert.Contains("Invoice", pdfText);
        Assert.Contains("Bill to", pdfText);
        Assert.Contains("Description", pdfText);
        Assert.Contains("Qty", pdfText);
        Assert.Contains("Rate", pdfText);
        Assert.Contains("Amount", pdfText);
        Assert.Contains("Total due", pdfText);
        Assert.Contains("Payment details", pdfText);
        Assert.Contains("Glovelly Music Ltd", pdfText);
        Assert.Contains("Performance fee for One-off corporate booking", pdfText);
        Assert.Contains(
            $"GBP {invoice.GetProperty("total").GetDecimal().ToString("0.00", CultureInfo.InvariantCulture)}",
            pdfText);
    }

    [Fact]
    public async Task GenerateInvoice_FromGig_WhenAlreadyInvoiced_ReturnsConflict()
    {
        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Already linked booking",
            date = "2026-06-21",
            venue = "Town Hall",
            fee = 280.00m,
            travelMiles = 0m,
            notes = "First invoice should win",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });

        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var firstResponse = await _client.PostAsync($"/gigs/{gigId}/generate-invoice", content: null);
        firstResponse.EnsureSuccessStatusCode();

        var duplicateResponse = await _client.PostAsync($"/gigs/{gigId}/generate-invoice", content: null);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var conflict = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("This gig has already been invoiced.", conflict.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.String, conflict.GetProperty("invoiceId").ValueKind);
    }

    [Fact]
    public async Task GenerateInvoice_FromGig_UsesUserDefaultPaymentWindow()
    {
        var updateSettingsResponse = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            defaultPaymentWindowDays = 30,
            invoiceFilenamePattern = "{InvoiceNumber}",
            invoiceReplyToEmail = (string?)null,
        });
        updateSettingsResponse.EnsureSuccessStatusCode();

        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Payment window booking",
            date = "2026-06-21",
            venue = "The Hall",
            fee = 300.00m,
            travelMiles = 0m,
            passengerCount = 0,
            notes = "",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var generateResponse = await _client.PostAsync(
            $"/gigs/{createdGig.GetProperty("id").GetGuid()}/generate-invoice",
            content: null);

        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);

        var invoice = await generateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var expectedInvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(
            expectedInvoiceDate.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            invoice.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task GenerateInvoice_FromSelectedGigs_CreatesCombinedInvoiceOrderedByDate()
    {
        var firstGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Second date gig",
            date = "2026-05-20",
            venue = "Bridge Hall",
            fee = 200.00m,
            travelMiles = 0m,
            notes = "Later in month",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        firstGigResponse.EnsureSuccessStatusCode();
        var firstGig = await firstGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var secondGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "First date gig",
            date = "2026-05-10",
            venue = "Park Room",
            fee = 300.00m,
            travelMiles = 0m,
            notes = "Earlier in month",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        secondGigResponse.EnsureSuccessStatusCode();
        var secondGig = await secondGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await _client.PostAsJsonAsync("/gigs/generate-invoice", new
        {
            gigIds = new[]
            {
                firstGig.GetProperty("id").GetGuid(),
                secondGig.GetProperty("id").GetGuid(),
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(today, invoice.GetProperty("invoiceDate").GetString());
        Assert.Equal(
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            invoice.GetProperty("dueDate").GetString());

        var lines = invoice.GetProperty("lines").EnumerateArray().OrderBy(line => line.GetProperty("sortOrder").GetInt32()).ToArray();
        Assert.Equal(2, lines.Length);
        Assert.Contains("First date gig (2026-05-10)", lines[0].GetProperty("description").GetString());
        Assert.Contains("Second date gig (2026-05-20)", lines[1].GetProperty("description").GetString());

        var invoiceId = invoice.GetProperty("id").GetGuid();
        foreach (var gigId in new[] { firstGig.GetProperty("id").GetGuid(), secondGig.GetProperty("id").GetGuid() })
        {
            var gigResponse = await _client.GetAsync($"/gigs/{gigId}");
            gigResponse.EnsureSuccessStatusCode();
            var refreshedGig = await gigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
            Assert.Equal(invoiceId, refreshedGig.GetProperty("invoiceId").GetGuid());
        }

        var pdfResponse = await _client.GetAsync($"/invoices/{invoiceId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);

        var pdfText = Encoding.ASCII.GetString(await pdfResponse.Content.ReadAsByteArrayAsync());
        Assert.Contains("Description", pdfText);
        Assert.Contains("Qty", pdfText);
        Assert.Contains("Rate", pdfText);
        Assert.Contains("Amount", pdfText);
        Assert.Contains(@"First date gig \(2026-05-10\)", pdfText);
        Assert.Contains(@"Second date gig \(2026-05-20\)", pdfText);
    }

    [Fact]
    public async Task Redraft_AfterLinkingGigsToDraftInvoice_RegeneratesDownloadablePdf()
    {
        var firstGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Monthly draft first gig",
            date = "2026-05-12",
            venue = "Park Room",
            fee = 300.00m,
            travelMiles = 0m,
            notes = "First monthly draft line",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        firstGigResponse.EnsureSuccessStatusCode();
        var firstGig = await firstGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var secondGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Monthly draft second gig",
            date = "2026-05-18",
            venue = "Bridge Hall",
            fee = 200.00m,
            travelMiles = 0m,
            notes = "Second monthly draft line",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        secondGigResponse.EnsureSuccessStatusCode();
        var secondGig = await secondGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var createInvoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-MONTHLY-TEST",
            clientId = TestData.FoxAndFinchId,
            status = "Draft",
            description = "Monthly invoice for 2026-05.",
        });
        createInvoiceResponse.EnsureSuccessStatusCode();
        var createdInvoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var invoiceId = createdInvoice.GetProperty("id").GetGuid();

        foreach (var gig in new[] { firstGig, secondGig })
        {
            var linkResponse = await _client.PutAsJsonAsync($"/gigs/{gig.GetProperty("id").GetGuid()}", new
            {
                clientId = TestData.FoxAndFinchId,
                title = gig.GetProperty("title").GetString(),
                date = gig.GetProperty("date").GetString(),
                venue = gig.GetProperty("venue").GetString(),
                fee = gig.GetProperty("fee").GetDecimal(),
                travelMiles = gig.GetProperty("travelMiles").GetDecimal(),
                passengerCount = 0,
                notes = gig.GetProperty("notes").GetString(),
                wasDriving = gig.GetProperty("wasDriving").GetBoolean(),
                status = gig.GetProperty("status").GetString(),
                invoiceId,
                expenses = Array.Empty<object>(),
                invoicedAt = (string?)null,
            });
            linkResponse.EnsureSuccessStatusCode();
        }

        var redraftResponse = await _client.PostAsync($"/invoices/{invoiceId}/redraft", null);

        Assert.Equal(HttpStatusCode.OK, redraftResponse.StatusCode);
        var redraftedInvoice = await redraftResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.String, redraftedInvoice.GetProperty("pdfBlob").ValueKind);
        Assert.Equal(2, redraftedInvoice.GetProperty("lines").GetArrayLength());
        var lines = redraftedInvoice.GetProperty("lines").EnumerateArray().ToArray();
        Assert.Contains(lines, line => line.GetProperty("description").GetString()!.Contains("Monthly draft first gig"));
        Assert.Contains(lines, line => line.GetProperty("description").GetString()!.Contains("Monthly draft second gig"));

        var pdfResponse = await _client.GetAsync($"/invoices/{invoiceId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.True((await pdfResponse.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [Fact]
    public async Task GenerateInvoice_FromSelectedGigs_WhenDifferentClients_ReturnsValidationError()
    {
        var foxGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Fox gig",
            date = "2026-06-01",
            venue = "Venue A",
            fee = 100.00m,
            travelMiles = 0m,
            notes = "Fox notes",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        foxGigResponse.EnsureSuccessStatusCode();
        var foxGig = await foxGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var riversideGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.RiversideId,
            title = "Riverside gig",
            date = "2026-06-02",
            venue = "Venue B",
            fee = 120.00m,
            travelMiles = 0m,
            notes = "Riverside notes",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        riversideGigResponse.EnsureSuccessStatusCode();
        var riversideGig = await riversideGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var response = await _client.PostAsJsonAsync("/gigs/generate-invoice", new
        {
            gigIds = new[]
            {
                foxGig.GetProperty("id").GetGuid(),
                riversideGig.GetProperty("id").GetGuid(),
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Selected gigs must all belong to the same client.", problem.GetProperty("errors").GetProperty("gigIds")[0].GetString());
    }

    [Fact]
    public async Task DownloadMonthlyInvoicePdf_WithMonthTokens_UsesInvoicedMonth()
    {
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
            invoiceFilenamePattern = "{ClientName} {MonthName} {Year} {InvoiceNumber}",
        });
        updateClientResponse.EnsureSuccessStatusCode();

        var createInvoiceResponse = await _client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "GLV-MONTHLY-PERIOD",
            clientId = TestData.FoxAndFinchId,
            status = "Draft",
            description = "Monthly invoice for 2026-02.",
        });
        createInvoiceResponse.EnsureSuccessStatusCode();

        var createdInvoice = await createInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var invoiceId = createdInvoice.GetProperty("id").GetGuid();

        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "February monthly booking",
            date = "2026-02-14",
            venue = "Winter Hall",
            fee = 250.00m,
            travelMiles = 0m,
            notes = "February monthly period",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        createGigResponse.EnsureSuccessStatusCode();
        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var linkGigResponse = await _client.PutAsJsonAsync($"/gigs/{createdGig.GetProperty("id").GetGuid()}", new
        {
            clientId = TestData.FoxAndFinchId,
            title = createdGig.GetProperty("title").GetString(),
            date = createdGig.GetProperty("date").GetString(),
            venue = createdGig.GetProperty("venue").GetString(),
            fee = createdGig.GetProperty("fee").GetDecimal(),
            travelMiles = createdGig.GetProperty("travelMiles").GetDecimal(),
            passengerCount = 0,
            notes = createdGig.GetProperty("notes").GetString(),
            wasDriving = createdGig.GetProperty("wasDriving").GetBoolean(),
            status = createdGig.GetProperty("status").GetString(),
            invoiceId,
            expenses = Array.Empty<object>(),
            invoicedAt = (string?)null,
        });
        linkGigResponse.EnsureSuccessStatusCode();

        var redraftResponse = await _client.PostAsync($"/invoices/{invoiceId}/redraft", null);
        redraftResponse.EnsureSuccessStatusCode();

        var pdfResponse = await _client.GetAsync($"/invoices/{invoiceId}/pdf");

        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal(
            "Fox & Finch Events February 2026 GLV-MONTHLY-PERIOD.pdf",
            pdfResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task DownloadInvoicePdf_WithClientFilenamePattern_UsesResolvedSanitizedFilename()
    {
        var sellerProfileResponse = await _client.PutAsJsonAsync("/seller-profile", new
        {
            sellerName = "Glovelly Music Ltd",
            addressLine1 = "1 Chapel Street",
            city = "Manchester",
            country = "United Kingdom",
            email = "accounts@glovelly.test",
            accountName = "Glovelly Music Ltd",
            sortCode = "12-34-56",
            accountNumber = "12345678",
        });
        sellerProfileResponse.EnsureSuccessStatusCode();

        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Filename pattern booking",
            date = "2026-04-20",
            venue = "Albert Hall",
            fee = 250.00m,
            travelMiles = 0m,
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var generateInvoiceResponse = await _client.PostAsync(
            $"/gigs/{createdGig.GetProperty("id").GetGuid()}/generate-invoice",
            content: null);
        generateInvoiceResponse.EnsureSuccessStatusCode();

        var generatedInvoice = await generateInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

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
            invoiceFilenamePattern = "{ClientName}: {MonthName} {Year} {InvoiceNumber}",
        });
        updateClientResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"/invoices/{generatedInvoice.GetProperty("id").GetGuid()}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            $"Fox & Finch Events- April 2026 {generatedInvoice.GetProperty("invoiceNumber").GetString()}.pdf",
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task DownloadInvoicePdf_WithoutClientPattern_UsesUserDefaultFilenamePattern()
    {
        var sellerProfileResponse = await _client.PutAsJsonAsync("/seller-profile", new
        {
            sellerName = "Glovelly Music Ltd",
            addressLine1 = "1 Chapel Street",
            city = "Manchester",
            country = "United Kingdom",
            email = "accounts@glovelly.test",
            accountName = "Glovelly Music Ltd",
            sortCode = "12-34-56",
            accountNumber = "12345678",
        });
        sellerProfileResponse.EnsureSuccessStatusCode();

        var updateUserSettingsResponse = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "{MonthName} {Year} {InvoiceNumber}",
        });
        updateUserSettingsResponse.EnsureSuccessStatusCode();

        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.RiversideId,
            title = "User default filename booking",
            date = "2026-04-21",
            venue = "Riverside Hall",
            fee = 250.00m,
            travelMiles = 0m,
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });
        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var generateInvoiceResponse = await _client.PostAsync(
            $"/gigs/{createdGig.GetProperty("id").GetGuid()}/generate-invoice",
            content: null);
        generateInvoiceResponse.EnsureSuccessStatusCode();

        var generatedInvoice = await generateInvoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var response = await _client.GetAsync($"/invoices/{generatedInvoice.GetProperty("id").GetGuid()}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            $"April 2026 {generatedInvoice.GetProperty("invoiceNumber").GetString()}.pdf",
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }
}
