using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class InvoicePdfPublicationTests
{
    [Fact]
    public async Task PreparationDoesNotPublishAndLaterWritesNeverReplacePublishedBytes()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryBlobStore();
        var service = new InvoicePdfService(store, TimeProvider.System);
        var invoice = new Invoice { Id = Guid.NewGuid(), InvoiceNumber = "TEST-001" };
        await service.SaveGeneratedPdfAsync(invoice, Guid.NewGuid(), [1, 2, 3], true, token);
        var originalKey = invoice.PdfStorageKey;
        var candidate = await service.PrepareAsync(invoice, invoice.CreatedByUserId, [4, 5, 6], true, token);
        Assert.Equal(originalKey, invoice.PdfStorageKey);
        Assert.NotEqual(originalKey, candidate.StorageKey);
        var original = await store.OpenReadAsync(originalKey!, token);
        await using (original.Content)
        {
            using var bytes = new MemoryStream();
            await original.Content.CopyToAsync(bytes, token);
            Assert.Equal(new byte[] { 1, 2, 3 }, bytes.ToArray());
        }
        service.ApplyCandidate(invoice, candidate);
        Assert.Equal(candidate.StorageKey, invoice.PdfStorageKey);
        Assert.Equal("TEST-001.pdf", invoice.PdfFileName);
    }

    [Fact]
    public async Task CandidateCannotBeAppliedToAnotherRevisionOrLifecycle()
    {
        var token = TestContext.Current.CancellationToken;
        var service = new InvoicePdfService(new InMemoryBlobStore(), TimeProvider.System);
        var invoice = new Invoice { Id = Guid.NewGuid() };
        var candidate = await service.PrepareAsync(invoice, null, [1], true, token);
        invoice.DocumentRevision++;
        Assert.Throws<InvalidOperationException>(() => service.ApplyCandidate(invoice, candidate));
        invoice.DocumentRevision--;
        invoice.Status = InvoiceStatus.Issued;
        Assert.Throws<InvalidOperationException>(() => service.ApplyCandidate(invoice, candidate));
        Assert.Null(invoice.PdfStorageKey);
    }
}
