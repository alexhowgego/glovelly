using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

public sealed class DraftInvoicePdfWatermarkTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public DraftInvoicePdfWatermarkTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    public void Renderer_WatermarksEveryDraftPageAfterContent(int lineCount)
    {
        var invoice = new Invoice { InvoiceNumber = "GLV-WATERMARK", Status = InvoiceStatus.Draft };
        var lines = Enumerable.Range(1, lineCount).Select(index =>
            new InvoiceLine { SortOrder = index, Description = $"Performance {index}", Quantity = 1, UnitPrice = 125 }).ToList();
        var pdf = Encoding.ASCII.GetString(new InvoicePdfRenderer().RenderInvoicePdf(
            invoice, new Client { Name = "Client", Email = "client@example.test" }, null, lines, null));
        var pages = Regex.Matches(pdf, @"stream\n(.*?)endstream", RegexOptions.Singleline);
        Assert.NotEmpty(pages);
        if (lineCount > 1) Assert.True(pages.Count > 1);
        foreach (Match page in pages)
        {
            var content = page.Groups[1].Value;
            Assert.EndsWith("q\n/GSdraft gs\n0.45 g\nBT /F2 96 Tf 0.7071 0.7071 -0.7071 0.7071 175 290 Tm (DRAFT) Tj ET\nQ\n", content);
            Assert.Single(Regex.Matches(content, @"\(DRAFT\) Tj"));
        }
        Assert.Contains("(Performance 1)", pdf);
        Assert.Contains("(GBP 125.00)", pdf);
        Assert.Contains("/ca 0.22", pdf);
    }

    [Theory]
    [InlineData(InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Overdue)]
    [InlineData(InvoiceStatus.Cancelled)]
    public void Renderer_NonDraftDocumentsRemainClean(InvoiceStatus status)
    {
        var pdf = new InvoicePdfRenderer().RenderInvoicePdf(
            new Invoice { Status = status, FirstIssuedUtc = DateTimeOffset.UtcNow }, new Client(), null, [], null);
        Assert.DoesNotContain("(DRAFT)", Encoding.ASCII.GetString(pdf));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledInvoice_OnlyPreviouslyIssuedDocumentsAreClean(bool previouslyIssued)
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.RiversideInvoiceId;
        (await _client.PostAsync($"/invoices/{id}/regenerate-pdf", null, token)).EnsureSuccessStatusCode();
        if (previouslyIssued) await SetStatusAsync(id, "Issued");
        var cancelled = await SetStatusAsync(id, "Cancelled");
        Assert.Equal(previouslyIssued, cancelled.FirstIssuedUtc.HasValue);
        (await _client.PostAsync($"/invoices/{id}/regenerate-pdf", null, token)).EnsureSuccessStatusCode();
        var pdf = await PdfAsync(id);
        Assert.Equal(!previouslyIssued, pdf.Contains("(DRAFT)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lifecycle_IssueReissueAndIssueAgain_PreservesIdentityAndCorrectBytes()
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.RiversideInvoiceId;
        var regenerated = await _client.PostAsync($"/invoices/{id}/regenerate-pdf", null, token);
        regenerated.EnsureSuccessStatusCode();
        Assert.Contains("(DRAFT)", await PdfAsync(id));
        var issued = await SetStatusAsync(id, "Issued");
        Assert.False(issued.PdfHasDraftWatermark);
        Assert.DoesNotContain("(DRAFT)", await PdfAsync(id));
        var reissueResponse = await _client.PostAsync($"/invoices/{id}/reissue", null, token);
        reissueResponse.EnsureSuccessStatusCode();
        var reissued = (await reissueResponse.Content.ReadFromJsonAsync<Invoice>(JsonOptions, token))!;
        Assert.Equal(InvoiceStatus.Draft, reissued.Status);
        Assert.True(reissued.PdfHasDraftWatermark);
        Assert.Contains("(DRAFT)", await PdfAsync(id));
        var issuedAgain = await SetStatusAsync(id, "Issued");
        Assert.Equal(issued.FirstIssuedUtc, issuedAgain.FirstIssuedUtc);
        Assert.Equal(issued.FirstIssuedByUserId, issuedAgain.FirstIssuedByUserId);
        Assert.Equal(1, issuedAgain.ReissueCount);
        Assert.Equal(0, issuedAgain.DeliveryCount);
        Assert.DoesNotContain("(DRAFT)", await PdfAsync(id));
    }

    [Theory]
    [InlineData(null, InvoiceStatus.Draft)]
    [InlineData(false, InvoiceStatus.Draft)]
    [InlineData(null, InvoiceStatus.Cancelled)]
    [InlineData(false, InvoiceStatus.Cancelled)]
    public async Task LegacyOrCleanDraft_IsBlockedAtAllBoundariesAndExplicitlyRecoverable(bool? watermark, InvoiceStatus status)
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.RiversideInvoiceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.SingleAsync(value => value.Id == id, token);
            invoice.Status = status;
            invoice.DocumentState = InvoiceDocumentState.Current;
            invoice.PdfDocumentRevision = invoice.DocumentRevision;
            invoice.PdfHasDraftWatermark = watermark;
            await db.SaveChangesAsync(token);
        }
        var response = await _client.GetAsync($"/invoices/{id}/pdf", token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Regenerate", await response.Content.ReadAsStringAsync(token));
        var metadata = (await _client.GetFromJsonAsync<Invoice>($"/invoices/{id}", JsonOptions, token))!;
        Assert.False(metadata.IsPdfCurrent);
        foreach (var path in new[] { "email-review", "send-email", "publish/google-drive" })
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                (await _client.PostAsync($"/invoices/{id}/{path}", null, token)).StatusCode);
        }
        Assert.Empty(_factory.Emails.SentEmails);
        using (var scope = _factory.Services.CreateScope())
        {
            var unchanged = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.SingleAsync(value => value.Id == id, token);
            Assert.Equal(watermark, unchanged.PdfHasDraftWatermark);
            Assert.Equal(0, unchanged.DeliveryCount);
        }
        var recovered = await _client.PostAsync($"/invoices/{id}/regenerate-pdf", null, token);
        recovered.EnsureSuccessStatusCode();
        var updated = (await recovered.Content.ReadFromJsonAsync<Invoice>(JsonOptions, token))!;
        Assert.Equal(status, updated.Status);
        Assert.Null(updated.FirstIssuedUtc);
        Assert.Equal(0, updated.ReissueCount);
        Assert.Contains("(DRAFT)", await PdfAsync(id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoringCancelledInvoice_RegeneratesDraftWithoutChangingIssueHistory(bool generalUpdate)
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.RiversideInvoiceId;
        var issued = await SetStatusAsync(id, "Issued");
        await SetStatusAsync(id, "Cancelled");
        var restored = generalUpdate
            ? await _client.PutAsJsonAsync($"/invoices/{id}", new
            {
                issued.InvoiceNumber, issued.ClientId, issued.InvoiceDate, issued.DueDate,
                issued.Description, status = "Draft"
            }, token)
            : await _client.PutAsJsonAsync($"/invoices/{id}/status", new { status = "Draft" }, token);
        restored.EnsureSuccessStatusCode();
        var invoice = (await restored.Content.ReadFromJsonAsync<Invoice>(JsonOptions, token))!;
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.True(invoice.IsPdfCurrent);
        Assert.True(invoice.PdfHasDraftWatermark);
        Assert.Equal(issued.InvoiceDate, invoice.InvoiceDate);
        Assert.Equal(issued.DueDate, invoice.DueDate);
        Assert.Equal(issued.FirstIssuedUtc, invoice.FirstIssuedUtc);
        Assert.Equal(issued.ReissueCount, invoice.ReissueCount);
        Assert.Contains("(DRAFT)", await PdfAsync(id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmailDelivery_AttachesTheCurrentDraftOrIssuedPdf(bool issueBeforeSending)
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.RiversideInvoiceId;
        (await _client.PostAsync($"/invoices/{id}/regenerate-pdf", null, token)).EnsureSuccessStatusCode();
        if (issueBeforeSending) await SetStatusAsync(id, "Issued");
        var response = await _client.PostAsJsonAsync($"/invoices/{id}/send-email", new { includeReceipts = false }, token);
        if (!issueBeforeSending)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_factory.Emails.SentEmails);
            Assert.Contains("(DRAFT)", await PdfAsync(id));
            return;
        }
        response.EnsureSuccessStatusCode();
        var message = Assert.Single(_factory.Emails.SentEmails);
        var attachment = Assert.Single(message.Attachments);
        Assert.Equal(!issueBeforeSending, Encoding.ASCII.GetString(attachment.Content).Contains("(DRAFT)", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft, false)]
    [InlineData(InvoiceStatus.Cancelled, false)]
    [InlineData(InvoiceStatus.Cancelled, true)]
    public async Task ApplicationDelivery_BlocksDraftsAndAllCancelledInvoicesWithoutSideEffects(
        InvoiceStatus status, bool previouslyIssued)
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.RiversideInvoiceId;
        (await _client.PostAsync($"/invoices/{id}/regenerate-pdf", null, token)).EnsureSuccessStatusCode();
        if (previouslyIssued) await SetStatusAsync(id, "Issued");
        if (status == InvoiceStatus.Cancelled) await SetStatusAsync(id, "Cancelled");
        foreach (var path in new[] { "send-email", "publish/google-drive" })
        {
            var response = await _client.PostAsync($"/invoices/{id}/{path}", null, token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(status == InvoiceStatus.Cancelled ? "Restore" : "Issue",
                await response.Content.ReadAsStringAsync(token));
        }
        var review = await _client.PostAsync($"/invoices/{id}/email-review", null, token);
        Assert.Equal(status == InvoiceStatus.Draft ? HttpStatusCode.OK : HttpStatusCode.BadRequest, review.StatusCode);
        Assert.NotEmpty(await PdfAsync(id));
        Assert.Empty(_factory.Emails.SentEmails);
        var invoice = (await _client.GetFromJsonAsync<Invoice>($"/invoices/{id}", JsonOptions, token))!;
        Assert.Equal(status, invoice.Status);
        Assert.Equal(0, invoice.DeliveryCount);
        Assert.Null(invoice.LastDeliveredUtc);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Cancelled)]
    public async Task SharedDeliveryService_RejectsIneligibleStatesBeforeResolvingChannels(InvoiceStatus status)
    {
        var service = new InvoiceDeliveryService([], TimeProvider.System);
        var invoice = new Invoice { Status = status };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeliverAsync(
            InvoiceDeliveryChannel.Email, invoice, new Client(), null, null, "", null, null, "",
            new InvoiceEmailSenderIdentity("Glovelly", null, null), TestContext.Current.CancellationToken));
        Assert.Equal(InvoiceDeliveryPolicy.GetError(invoice), error.Message);
        Assert.Equal(0, invoice.DeliveryCount);
    }

    [Theory]
    [InlineData(InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Overdue)]
    public void DeliveryPolicy_AllowsIssuedPaidAndOverdue(InvoiceStatus status) =>
        Assert.Null(InvoiceDeliveryPolicy.GetError(new Invoice { Status = status }));

    [Fact]
    public async Task RestoringCancelledInvoice_RenderFailureSavesDraftAndOffersSafeRetry()
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.RiversideInvoiceId;
        var renderer = new ThrowingRenderer();
        using var failing = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInvoicePdfRenderer>();
            services.AddSingleton<IInvoicePdfRenderer>(renderer);
        }));
        using var client = failing.CreateClient();
        var firstIssued = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);
        using (var scope = failing.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.Include(value => value.Client).SingleAsync(value => value.Id == id, token);
            invoice.Status = InvoiceStatus.Cancelled;
            invoice.FirstIssuedUtc = firstIssued;
            var bytes = new InvoicePdfRenderer().RenderInvoicePdf(invoice, invoice.Client!, null, [], null);
            await scope.ServiceProvider.GetRequiredService<IInvoicePdfService>().SaveGeneratedPdfAsync(
                invoice, TestAuthContext.UserId, bytes, false, token);
            await db.SaveChangesAsync(token);
        }
        var response = await client.PutAsJsonAsync($"/invoices/{id}/status", new { status = "Draft" }, token);
        response.EnsureSuccessStatusCode();
        var restored = (await response.Content.ReadFromJsonAsync<Invoice>(JsonOptions, token))!;
        Assert.Equal(InvoiceStatus.Draft, restored.Status);
        Assert.Equal(InvoiceDocumentState.Failed, restored.DocumentState);
        Assert.Equal(firstIssued, restored.FirstIssuedUtc);
        Assert.False(restored.IsPdfCurrent);
        Assert.Equal(0, restored.ReissueCount);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/invoices/{id}/pdf", token)).StatusCode);
        renderer.ShouldThrow = false;
        var retry = await client.PostAsync($"/invoices/{id}/regenerate-pdf", null, token);
        retry.EnsureSuccessStatusCode();
        Assert.True((await retry.Content.ReadFromJsonAsync<Invoice>(JsonOptions, token))!.IsPdfCurrent);
        Assert.Contains("(DRAFT)", Encoding.ASCII.GetString(await client.GetByteArrayAsync($"/invoices/{id}/pdf", token)));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task IssuedDocument_UsesCompatibleStoredProvenance(bool? watermark, bool allowed)
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.FoxInvoiceId;
        (await _client.PostAsync($"/invoices/{id}/regenerate-pdf", null, token)).EnsureSuccessStatusCode();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.SingleAsync(value => value.Id == id, token);
            invoice.PdfHasDraftWatermark = watermark;
            await db.SaveChangesAsync(token);
        }
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
            (await _client.GetAsync($"/invoices/{id}/pdf", token)).StatusCode);
    }

    [Theory]
    [InlineData("Draft", "status")]
    [InlineData("Issued", "reissue")]
    [InlineData("Draft", "regenerate-pdf")]
    public async Task FailedRendering_DoesNotPublishIncompatibleReplacement(string initialStatus, string action)
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.RiversideInvoiceId;
        var renderer = new ThrowingRenderer();
        using var failing = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInvoicePdfRenderer>();
            services.AddSingleton<IInvoicePdfRenderer>(renderer);
        }));
        using var client = failing.CreateClient();
        // Prepare a real current PDF without invoking the failing renderer.
        byte[] before;
        using (var scope = failing.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.Include(value => value.Client).Include(value => value.Lines)
                .SingleAsync(value => value.Id == id, token);
            invoice.Status = Enum.Parse<InvoiceStatus>(initialStatus);
            before = new InvoicePdfRenderer().RenderInvoicePdf(invoice, invoice.Client!, null, invoice.Lines.ToList(), null);
            await scope.ServiceProvider.GetRequiredService<IInvoicePdfService>().SaveGeneratedPdfAsync(
                invoice, TestAuthContext.UserId, before, invoice.RequiresDraftWatermark, token);
            await db.SaveChangesAsync(token);
        }
        if (action == "regenerate-pdf")
        {
            (await client.PostAsync($"/invoices/{id}/{action}", null, token)).EnsureSuccessStatusCode();
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                if (action == "status")
                    await client.PutAsJsonAsync($"/invoices/{id}/status", new { status = "Issued" }, token);
                else
                    await client.PostAsync($"/invoices/{id}/{action}", null, token);
            });
        }
        using var check = failing.Services.CreateScope();
        var persisted = await check.ServiceProvider.GetRequiredService<AppDbContext>().Invoices.SingleAsync(value => value.Id == id, token);
        Assert.Equal(Enum.Parse<InvoiceStatus>(initialStatus), persisted.Status);
        Assert.Null(persisted.FirstIssuedUtc);
        Assert.Equal(0, persisted.ReissueCount);
        if (action == "regenerate-pdf")
        {
            Assert.False(InvoiceDocumentReadiness.IsCurrent(persisted));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/invoices/{id}/pdf", token)).StatusCode);
        }
        else
        {
            Assert.True(InvoiceDocumentReadiness.IsCurrent(persisted));
            Assert.Equal(before, await client.GetByteArrayAsync($"/invoices/{id}/pdf", token));
        }
        renderer.ShouldThrow = false;
        var retry = action == "status"
            ? await client.PutAsJsonAsync($"/invoices/{id}/status", new { status = "Issued" }, token)
            : await client.PostAsync($"/invoices/{id}/{action}", null, token);
        retry.EnsureSuccessStatusCode();
        var recovered = (await retry.Content.ReadFromJsonAsync<Invoice>(JsonOptions, token))!;
        Assert.True(recovered.IsPdfCurrent);
        Assert.Equal(action != "status", recovered.PdfHasDraftWatermark);
    }

    [Fact]
    public async Task WatermarkReadAndRecovery_RespectOwnerVisibility()
    {
        var token = TestContext.Current.CancellationToken;
        var id = TestData.RiversideInvoiceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.SingleAsync(value => value.Id == id, token);
            invoice.CreatedByUserId = Guid.NewGuid();
            await db.SaveChangesAsync(token);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/invoices/{id}/pdf", token)).StatusCode);
        foreach (var path in new[] { "regenerate-pdf", "email-review", "send-email", "publish/google-drive" })
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/invoices/{id}/{path}", null, token)).StatusCode);
    }

    [Fact]
    public async Task FailedStorage_DoesNotStampNewProvenanceAndCanBeRetried()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FailingBlobStore();
        var service = new InvoicePdfService(store, TimeProvider.System);
        var invoice = new Invoice { Id = Guid.NewGuid(), Status = InvoiceStatus.Draft };
        var content = new InvoicePdfRenderer().RenderInvoicePdf(invoice, new Client(), null, [], null);
        await Assert.ThrowsAsync<IOException>(() => service.SaveGeneratedPdfAsync(invoice, null, content, true, token));
        Assert.Null(invoice.PdfHasDraftWatermark);
        Assert.Null(invoice.PdfDocumentRevision);
        Assert.False(invoice.IsPdfCurrent);
        Assert.False((await service.OpenCurrentReadAsync(invoice, token)).IsAvailable);
        store.ShouldThrow = false;
        await service.SaveGeneratedPdfAsync(invoice, null, content, true, token);
        Assert.True(invoice.PdfHasDraftWatermark);
        Assert.True(invoice.IsPdfCurrent);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveGeneratedPdfAsync(invoice, null, content, false, token));
        Assert.True(invoice.PdfHasDraftWatermark);
    }

    private async Task<string> PdfAsync(Guid id)
    {
        var response = await _client.GetAsync($"/invoices/{id}/pdf", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    private async Task<Invoice> SetStatusAsync(Guid id, string status)
    {
        var response = await _client.PutAsJsonAsync($"/invoices/{id}/status", new { status }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Invoice>(JsonOptions, TestContext.Current.CancellationToken))!;
    }

    private sealed class ThrowingRenderer : IInvoicePdfRenderer
    {
        public bool ShouldThrow { get; set; } = true;
        public byte[] RenderInvoicePdf(Invoice invoice, Client client, Gig? gig,
            IReadOnlyCollection<InvoiceLine> lines, SellerProfile? sellerProfile) =>
            ShouldThrow ? throw new InvalidOperationException("PDF rendering unavailable.") :
                new InvoicePdfRenderer().RenderInvoicePdf(invoice, client, gig, lines, sellerProfile);
    }

    private sealed class FailingBlobStore : IBlobStore
    {
        private readonly InMemoryBlobStore _inner = new();
        public bool ShouldThrow { get; set; } = true;
        public Task SaveAsync(BlobWriteRequest request, CancellationToken cancellationToken = default) =>
            ShouldThrow ? throw new IOException("Blob storage unavailable.") : _inner.SaveAsync(request, cancellationToken);
        public Task<BlobReadResult> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
            _inner.OpenReadAsync(key, cancellationToken);
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(key, cancellationToken);
    }
}
