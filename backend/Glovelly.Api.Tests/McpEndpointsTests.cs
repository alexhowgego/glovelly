using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Glovelly.Api.Services;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class McpEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public McpEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OptionsMcp_ReturnsChatGptCompatibleCorsHeaders()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/mcp");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Contains("authorization", response.Headers.GetValues("Access-Control-Allow-Headers").Single());
        Assert.Contains("mcp-session-id", response.Headers.GetValues("Access-Control-Allow-Headers").Single());
        Assert.Contains("Mcp-Session-Id", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
    }

    [Fact]
    public async Task GetMcp_WhenServerDoesNotOfferSseStream_ReturnsMethodNotAllowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("Mcp-Session-Id", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
    }

    [Fact]
    public async Task InitializedNotification_ReturnsAcceptedWithoutBody()
    {
        var response = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            method = "notifications/initialized",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task Initialize_NegotiatesCurrentProtocolVersion()
    {
        var response = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new
                {
                    name = "test-client",
                    version = "1.0.0",
                },
            },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2025-06-18", response.Headers.GetValues("MCP-Protocol-Version").Single());

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("2025-06-18", payload.GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.Equal("glovelly", payload.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.True(payload.GetProperty("result").GetProperty("capabilities").TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task PostMcp_WithAnonymousDevelopmentAuthDisabled_ReturnsUnauthorized()
    {
        using var factory = CreateMcpDevelopmentFactory();
        using var client = factory.CreateClient();
        using var request = CreateMcpRequest(ListInvoicesPayload());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("resource_metadata=", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task OAuthProtectedResourceMetadata_AdvertisesAuthorizationServer()
    {
        using var factory = CreateMcpOAuthFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("https://glovelly.test/mcp", payload.GetProperty("resource").GetString());
        Assert.Equal(
            "https://glovelly.test",
            payload.GetProperty("authorization_servers").EnumerateArray().Single().GetString());
        Assert.Equal("mcp:read", payload.GetProperty("scopes_supported").EnumerateArray().Single().GetString());
    }

    [Fact]
    public async Task OAuthAuthorizationServerMetadata_AdvertisesCodeFlowWithPkce()
    {
        using var factory = CreateMcpOAuthFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-authorization-server");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("https://glovelly.test", payload.GetProperty("issuer").GetString());
        Assert.Equal("https://glovelly.test/oauth/authorize", payload.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("https://glovelly.test/oauth/token", payload.GetProperty("token_endpoint").GetString());
        Assert.Contains(
            payload.GetProperty("code_challenge_methods_supported").EnumerateArray(),
            value => value.GetString() == "S256");
    }

    [Fact]
    public async Task PostMcp_WithOAuthBearerToken_AuthenticatesTokenUser()
    {
        using var factory = CreateMcpOAuthFactory();
        using var client = factory.CreateClient();
        var codeVerifier = "test-code-verifier-with-enough-entropy";
        var codeChallenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        string authorizationCode;

        using (var scope = factory.Services.CreateScope())
        {
            var oauthService = scope.ServiceProvider.GetRequiredService<IMcpOAuthService>();
            var issuedCode = await oauthService.CreateAuthorizationCodeAsync(
                "chatgpt-test",
                TestAuthContext.UserId,
                "https://chatgpt.test/callback",
                "mcp:read",
                "https://glovelly.test/mcp",
                codeChallenge,
                "S256",
                CancellationToken.None);
            authorizationCode = issuedCode.Code;
        }

        var tokenResponse = await client.PostAsync(
            "/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = "chatgpt-test",
                ["client_secret"] = "secret-test",
                ["code"] = authorizationCode,
                ["redirect_uri"] = "https://chatgpt.test/callback",
                ["code_verifier"] = codeVerifier,
                ["resource"] = "https://glovelly.test/mcp",
            }));

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = tokenPayload.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        using var request = CreateMcpRequest(ListInvoicesPayload());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.NotEmpty(payload
            .GetProperty("result")
            .GetProperty("structuredContent")
            .GetProperty("invoices")
            .EnumerateArray());
    }

    [Fact]
    public async Task PostMcp_WithAnonymousDevelopmentAuth_AuthenticatesConfiguredUser()
    {
        using var factory = CreateMcpDevelopmentFactory(allowAnonymous: true);
        using var client = factory.CreateClient();
        using var request = CreateMcpRequest(ListInvoicesPayload());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var result = payload
            .GetProperty("result")
            .GetProperty("structuredContent");

        Assert.NotEmpty(result.GetProperty("invoices").EnumerateArray());
    }

    [Fact]
    public async Task PostMcp_WithAnonymousDevelopmentAuthMissingUserId_ReturnsUnauthorized()
    {
        using var factory = new GlovellyApiFactory().WithConfiguration(new Dictionary<string, string?>
        {
            ["Mcp:DevelopmentAuth:AllowAnonymous"] = "true",
        });
        using var client = factory.CreateClient();
        using var request = CreateMcpRequest(ListInvoicesPayload());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostMcp_WithUnsupportedProtocolHeader_ReturnsBadRequest()
    {
        using var request = CreateMcpRequest(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "ping",
        });
        request.Headers.Remove("MCP-Protocol-Version");
        request.Headers.Add("MCP-Protocol-Version", "1900-01-01");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ToolsList_ReturnsExperimentalGlovellyTools()
    {
        var response = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var toolNames = payload
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("glovelly_search_contacts", toolNames);
        Assert.Contains("glovelly_list_invoices", toolNames);
        Assert.Contains("glovelly_get_invoice", toolNames);
        Assert.Contains("glovelly_list_receipts", toolNames);
        Assert.Contains("glovelly_get_business_summary", toolNames);
    }

    [Fact]
    public async Task ListInvoices_CanReturnOutstandingInvoicesForDateRange()
    {
        await CreateInvoiceLineAsync(TestData.FoxInvoiceId, 450m);

        var result = await CallToolAsync("glovelly_list_invoices", new
        {
            status = "outstanding",
            fromDate = "2026-04-01",
            toDate = "2026-04-30",
            dateBasis = "issueDate",
        });

        Assert.False(result.GetProperty("ambiguous").GetBoolean());
        Assert.Equal(450m, result.GetProperty("totalOutstanding").GetDecimal());

        var invoice = Assert.Single(result.GetProperty("invoices").EnumerateArray());
        Assert.Equal(TestData.FoxInvoiceId, invoice.GetProperty("invoiceId").GetGuid());
        Assert.Equal("Fox & Finch Events", invoice.GetProperty("contactName").GetString());
        Assert.Equal("issued", invoice.GetProperty("status").GetString());
        Assert.Equal(450m, invoice.GetProperty("outstandingAmount").GetDecimal());
    }

    [Fact]
    public async Task ListInvoices_CanFilterByResolvedContactQuery()
    {
        await CreateInvoiceLineAsync(TestData.FoxInvoiceId, 200m);
        await CreateInvoiceLineAsync(TestData.RiversideInvoiceId, 300m);

        var issueResponse = await _client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}/status", new
        {
            status = "Issued",
        });
        issueResponse.EnsureSuccessStatusCode();

        var result = await CallToolAsync("glovelly_list_invoices", new
        {
            contactQuery = "Riverside",
            status = "outstanding",
        });

        var invoice = Assert.Single(result.GetProperty("invoices").EnumerateArray());
        Assert.Equal(TestData.RiversideInvoiceId, invoice.GetProperty("invoiceId").GetGuid());
        Assert.Equal("Riverside Arts Centre", invoice.GetProperty("contactName").GetString());
    }

    [Fact]
    public async Task ListInvoices_WhenContactQueryIsAmbiguous_ReturnsMatchesWithoutGuessing()
    {
        var createClientResponse = await _client.PostAsJsonAsync("/clients", new
        {
            name = "Fox Theatre",
            email = "accounts@foxtheatre.test",
            billingAddress = new { },
        });
        createClientResponse.EnsureSuccessStatusCode();

        var result = await CallToolAsync("glovelly_list_invoices", new
        {
            contactQuery = "Fox",
            status = "outstanding",
        });

        Assert.True(result.GetProperty("ambiguous").GetBoolean());
        Assert.Empty(result.GetProperty("invoices").EnumerateArray());
        Assert.True(result.GetProperty("matches").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task ListInvoices_WhenContactQueryHasNoMatches_ReturnsEmptyList()
    {
        var result = await CallToolAsync("glovelly_list_invoices", new
        {
            contactQuery = "No such contact",
            status = "outstanding",
        });

        Assert.False(result.GetProperty("ambiguous").GetBoolean());
        Assert.Empty(result.GetProperty("invoices").EnumerateArray());
    }

    [Fact]
    public async Task GetInvoice_WhenInvoiceExists_ReturnsInvoiceDetail()
    {
        await CreateInvoiceLineAsync(TestData.FoxInvoiceId, 125m);

        var result = await CallToolAsync("glovelly_get_invoice", new
        {
            invoiceId = TestData.FoxInvoiceId,
        });

        Assert.True(result.GetProperty("found").GetBoolean());

        var invoice = result.GetProperty("invoice");
        Assert.Equal(TestData.FoxInvoiceId, invoice.GetProperty("invoiceId").GetGuid());
        Assert.Equal(125m, invoice.GetProperty("total").GetDecimal());
        Assert.Single(invoice.GetProperty("lines").EnumerateArray());
    }

    [Fact]
    public async Task ListInvoices_WhenAuthenticatedAsAnotherUser_DoesNotExposeOtherUserInvoices()
    {
        await CreateInvoiceLineAsync(TestData.FoxInvoiceId, 125m);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new
                {
                    name = "glovelly_list_invoices",
                    arguments = new
                    {
                        status = "outstanding",
                    },
                },
            }),
        };
        request.Headers.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var result = payload
            .GetProperty("result")
            .GetProperty("structuredContent");

        Assert.Empty(result.GetProperty("invoices").EnumerateArray());
        Assert.Equal(0m, result.GetProperty("totalOutstanding").GetDecimal());
    }

    [Fact]
    public async Task ListReceipts_ReturnsReadOnlyReceiptExpenses()
    {
        await CreateGigAsync("Receipt gig", new[]
        {
            new { sortOrder = 1, description = "Receipt draft", amount = 0m },
            new { sortOrder = 2, description = "Parking", amount = 12m },
        });

        var result = await CallToolAsync("glovelly_list_receipts", new
        {
            fromDate = "2026-04-01",
            toDate = "2026-04-30",
            status = "unmatched",
        });

        Assert.Equal(1, result.GetProperty("receiptCount").GetInt32());
        Assert.Equal(1, result.GetProperty("unmatchedReceiptCount").GetInt32());

        var receipt = Assert.Single(result.GetProperty("receipts").EnumerateArray());
        Assert.Equal("Receipt draft", receipt.GetProperty("description").GetString());
        Assert.Equal("unmatched", receipt.GetProperty("status").GetString());
    }

    [Fact]
    public async Task BusinessSummary_ReturnsPeriodTotals()
    {
        await CreateInvoiceLineAsync(TestData.FoxInvoiceId, 150m);
        await CreateGigAsync("Summary gig", new[]
        {
            new { sortOrder = 1, description = "Train", amount = 24m },
        });

        var result = await CallToolAsync("glovelly_get_business_summary", new
        {
            fromDate = "2026-04-01",
            toDate = "2026-04-30",
        });

        Assert.Equal(150m, result.GetProperty("invoiceTotal").GetDecimal());
        Assert.Equal(150m, result.GetProperty("outstandingTotal").GetDecimal());
        Assert.Equal(24m, result.GetProperty("expenseTotal").GetDecimal());
        Assert.Equal(1, result.GetProperty("receiptCount").GetInt32());
    }

    private async Task<JsonElement> CallToolAsync(string name, object arguments)
    {
        var response = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name,
                arguments,
            },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        if (payload.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException(error.GetRawText());
        }

        return payload
            .GetProperty("result")
            .GetProperty("structuredContent");
    }

    private Task<HttpResponseMessage> PostMcpAsync(object payload)
    {
        return _client.SendAsync(CreateMcpRequest(payload));
    }

    private static HttpRequestMessage CreateMcpRequest(object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("MCP-Protocol-Version", "2025-06-18");

        return request;
    }

    private static object ListInvoicesPayload()
    {
        return new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "glovelly_list_invoices",
                arguments = new
                {
                    status = "outstanding",
                },
            },
        };
    }

    private static GlovellyApiFactory CreateMcpDevelopmentFactory(bool allowAnonymous = false)
    {
        return new GlovellyApiFactory().WithConfiguration(new Dictionary<string, string?>
        {
            ["DevelopmentSeeding:AdminGoogleSubject"] = TestAuthContext.DefaultSubject,
            ["Mcp:DevelopmentAuth:AllowAnonymous"] = allowAnonymous.ToString(),
        });
    }

    private static GlovellyApiFactory CreateMcpOAuthFactory()
    {
        return new GlovellyApiFactory().WithConfiguration(new Dictionary<string, string?>
        {
            ["Mcp:OAuth:Issuer"] = "https://glovelly.test",
            ["Mcp:OAuth:Resource"] = "https://glovelly.test/mcp",
            ["Mcp:OAuth:Clients:0:ClientId"] = "chatgpt-test",
            ["Mcp:OAuth:Clients:0:ClientSecret"] = "secret-test",
            ["Mcp:OAuth:Clients:0:DisplayName"] = "ChatGPT Test",
            ["Mcp:OAuth:Clients:0:RedirectUris:0"] = "https://chatgpt.test/callback",
            ["Mcp:OAuth:Clients:0:Scopes:0"] = "mcp:read",
        });
    }

    private async Task CreateInvoiceLineAsync(Guid invoiceId, decimal amount)
    {
        var response = await _client.PostAsJsonAsync("/invoice-lines", new
        {
            invoiceId,
            sortOrder = 1,
            type = InvoiceLineType.PerformanceFee,
            description = "Performance",
            quantity = 1m,
            unitPrice = amount,
        });

        response.EnsureSuccessStatusCode();
    }

    private async Task CreateGigAsync(string title, object[] expenses)
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title,
            date = "2026-04-12",
            venue = "Test venue",
            fee = 100m,
            travelMiles = 0m,
            wasDriving = false,
            status = GigStatus.Confirmed,
            expenses,
        });

        response.EnsureSuccessStatusCode();
    }
}
