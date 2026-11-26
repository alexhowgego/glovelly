using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class EntitlementReportingTests
{
    [Fact]
    public async Task FreeCannotReadSummaryButKeepsRecordsPaidStatusAndDraftDownload()
    {
        var token = TestContext.Current.CancellationToken;
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        await SetFreeAsync(factory);
        using var summary = await client.GetAsync("/invoices/paid-income-summary", token);
        Assert.Equal(HttpStatusCode.Forbidden, summary.StatusCode);
        var problem = await summary.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("feature_not_entitled", problem.GetProperty("code").GetString());
        Assert.False(problem.TryGetProperty("total", out _));
        Assert.False(problem.TryGetProperty("invoiceIds", out _));
        (await client.GetAsync("/invoices", token)).EnsureSuccessStatusCode();
        (await client.GetAsync($"/invoices/{TestData.FoxInvoiceId}", token)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/invoices/{TestData.FoxInvoiceId}/status", new { status = "Paid" }, token))
            .EnsureSuccessStatusCode();
        (await client.PostAsync($"/invoices/{TestData.RiversideInvoiceId}/redraft", null, token)).EnsureSuccessStatusCode();
        (await client.GetAsync($"/invoices/{TestData.RiversideInvoiceId}/pdf", token)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task McpReportingReturnsFeatureToolErrorButRawInvoiceToolsRemainAvailable()
    {
        var token = TestContext.Current.CancellationToken;
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        await SetFreeAsync(factory);
        foreach (var (name, restricted) in new[] { ("glovelly_get_business_summary", true), ("glovelly_list_invoices", false) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = JsonContent.Create(new
                {
                    jsonrpc = "2.0", id = 1, method = "tools/call",
                    @params = new { name, arguments = new { } },
                }),
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");
            request.Headers.Add("MCP-Protocol-Version", "2025-06-18");
            using var response = await client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(token);
            var result = payload.GetProperty("result");
            Assert.Equal(restricted, result.GetProperty("isError").GetBoolean());
            if (restricted)
            {
                var content = result.GetProperty("structuredContent");
                Assert.Equal("feature_not_entitled", content.GetProperty("code").GetString());
                Assert.False(content.TryGetProperty("paidIncome", out _));
            }
        }
    }

    private static async Task SetFreeAsync(GlovellyApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserSubscriptions.SingleAsync(TestContext.Current.CancellationToken)).PlanCode = Plan.Free;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
