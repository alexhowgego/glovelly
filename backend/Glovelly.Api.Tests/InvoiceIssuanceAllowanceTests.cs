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

public sealed class InvoiceIssuanceAllowanceTests
{
    [Fact]
    public async Task ThirdFirstIssueIsDeniedWithoutPublishingOrConsuming()
    {
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        await AssignAsync(factory, Plan.Free);
        for (var index = 0; index < 2; index++)
        {
            var id = await CreateDraftAsync(client);
            using var issued = await client.PutAsJsonAsync($"/invoices/{id}/status", new { status = "Issued" },
                TestContext.Current.CancellationToken);
            issued.EnsureSuccessStatusCode();
        }
        var deniedId = await CreateDraftAsync(client);
        using var redraft = await client.PostAsync($"/invoices/{deniedId}/redraft", null, TestContext.Current.CancellationToken);
        redraft.EnsureSuccessStatusCode();
        var draft = await redraft.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        using var denied = await client.PutAsJsonAsync($"/invoices/{deniedId}/status", new { status = "Issued" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var problem = await denied.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("invoice_allowance_exhausted", problem.GetProperty("code").GetString());
        Assert.Equal(2, problem.GetProperty("usage").GetInt32());
        Assert.Equal(0, problem.GetProperty("remaining").GetInt32());
        var retained = await client.GetFromJsonAsync<JsonElement>($"/invoices/{deniedId}", TestContext.Current.CancellationToken);
        Assert.Equal("Draft", retained.GetProperty("status").GetString());
        Assert.Equal(draft.GetProperty("pdfStorageKey").GetString(), retained.GetProperty("pdfStorageKey").GetString());
        Assert.Equal(JsonValueKind.Null, retained.GetProperty("firstIssuedUtc").ValueKind);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.UsageLedgerEntries.CountAsync(
            value => value.OccurredAtUtc.Year == 2026 && value.OccurredAtUtc.Month == 1,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnlimitedUsageSurvivesDowngradeAndCorrectionsRemainFree()
    {
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        var ids = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            var id = await CreateDraftAsync(client);
            ids.Add(id);
            (await client.PutAsJsonAsync($"/invoices/{id}/status", new { status = "Issued" },
                TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        }
        await AssignAsync(factory, Plan.Free);
        var newId = await CreateDraftAsync(client);
        using var denied = await client.PutAsJsonAsync($"/invoices/{newId}/status", new { status = "Issued" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        (await client.PostAsync($"/invoices/{ids[0]}/reissue", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/invoices/{ids[0]}/status", new { status = "Issued" },
            TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/invoices/{ids[0]}/reissue", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/invoices/{ids[0]}", TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await db.UsageLedgerEntries.CountAsync(
            value => value.OccurredAtUtc.Month == 1, TestContext.Current.CancellationToken));
        Assert.True(await db.UsageLedgerEntries.AnyAsync(
            value => value.SourceInvoiceId == ids[0], TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Issued")]
    [InlineData("Paid")]
    [InlineData("Overdue")]
    [InlineData("Cancelled")]
    public async Task NonDraftCreationIsRejected(string status)
    {
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = "FORGED", clientId = TestData.FoxAndFinchId, status,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("firstIssuedUtc", "2026-01-01T00:00:00Z")]
    [InlineData("firstIssuedByUserId", "11111111-1111-1111-1111-111111111111")]
    [InlineData("pdfStorageKey", "forged")]
    [InlineData("sourceInvoiceId", "11111111-1111-1111-1111-111111111111")]
    public async Task ProtectedCreateAndUpdateFieldsAreRejected(string field, string value)
    {
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        var payload = new Dictionary<string, object?>
        {
            ["invoiceNumber"] = "FORGED", ["clientId"] = TestData.FoxAndFinchId,
            ["status"] = "Draft", [field] = value,
        };
        using var create = await client.PostAsJsonAsync("/invoices", payload, TestContext.Current.CancellationToken);
        using var update = await client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}", payload,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task GenericUpdateCannotBypassAllowance()
    {
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        await AssignAsync(factory, Plan.Free);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UsageLedgerEntries.Add(new UsageLedgerEntry
            {
                Id = Guid.NewGuid(), UserId = TestAuthContext.UserId, SourceInvoiceId = Guid.NewGuid(),
                Quantity = 2, OccurredAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z"), Source = "Test",
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var response = await client.PutAsJsonAsync($"/invoices/{TestData.RiversideInvoiceId}", new
        {
            invoiceNumber = "GENERIC", clientId = TestData.RiversideId, status = "Issued",
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<Guid> CreateDraftAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/invoices", new
        {
            invoiceNumber = $"TEST-{Guid.NewGuid():N}", clientId = TestData.FoxAndFinchId, status = "Draft",
        }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var invoice = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return invoice.GetProperty("id").GetGuid();
    }

    private static async Task AssignAsync(GlovellyApiFactory factory, string plan)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var grant = await db.UserSubscriptions.SingleAsync(TestContext.Current.CancellationToken);
        grant.PlanCode = plan;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
