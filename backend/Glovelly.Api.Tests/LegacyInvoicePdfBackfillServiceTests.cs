using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class LegacyInvoicePdfBackfillServiceTests
{
    [Fact]
    public async Task BackfillAsync_CopiesLegacyPdfBytesToBlobStorageAndKeepsPdfBlob()
    {
        var now = new DateTimeOffset(2026, 5, 17, 20, 30, 0, TimeSpan.Zero);
        var ownerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var invoiceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var pdfBytes = "%PDF legacy exact bytes"u8.ToArray();
        await using var dbContext = CreateDbContext();
        var blobStore = new InMemoryBlobStore();
        dbContext.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            InvoiceNumber = "GLV-118-001",
            ClientId = Guid.NewGuid(),
            CreatedByUserId = ownerId,
            InvoiceDate = new DateOnly(2026, 5, 17),
            DueDate = new DateOnly(2026, 5, 31),
            Status = InvoiceStatus.Issued,
            FirstIssuedUtc = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero),
            PdfBlob = pdfBytes
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, blobStore, now);

        await service.BackfillAsync();

        var invoice = await dbContext.Invoices.SingleAsync();
        var expectedKey = InvoicePdfStorage.BuildStorageKey(invoice, ownerId);
        Assert.Equal(expectedKey, invoice.PdfStorageKey);
        Assert.Equal("GLV-118-001.pdf", invoice.PdfFileName);
        Assert.Equal("application/pdf", invoice.PdfContentType);
        Assert.Equal(pdfBytes.Length, invoice.PdfSizeBytes);
        Assert.Equal(now, invoice.PdfGeneratedAt);
        Assert.Equal(pdfBytes, invoice.PdfBlob);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero), invoice.FirstIssuedUtc);

        await using var storedPdf = (await blobStore.OpenReadAsync(expectedKey)).Content;
        using var memory = new MemoryStream();
        await storedPdf.CopyToAsync(memory);
        Assert.Equal(pdfBytes, memory.ToArray());
    }

    [Fact]
    public async Task BackfillAsync_SkipsInvoicesThatAlreadyHaveStorageKeys()
    {
        var ownerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var invoiceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        await using var dbContext = CreateDbContext();
        var blobStore = new InMemoryBlobStore();
        dbContext.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            InvoiceNumber = "GLV-118-002",
            ClientId = Guid.NewGuid(),
            CreatedByUserId = ownerId,
            InvoiceDate = new DateOnly(2026, 5, 17),
            DueDate = new DateOnly(2026, 5, 31),
            PdfBlob = "%PDF should stay only legacy"u8.ToArray(),
            PdfStorageKey = "users/existing/invoices/existing/invoice.pdf",
            PdfFileName = "existing.pdf",
            PdfContentType = "application/pdf",
            PdfSizeBytes = 12,
            PdfGeneratedAt = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, blobStore, DateTimeOffset.UtcNow);

        await service.BackfillAsync();

        var invoice = await dbContext.Invoices.SingleAsync();
        Assert.Equal("users/existing/invoices/existing/invoice.pdf", invoice.PdfStorageKey);
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => blobStore.OpenReadAsync(InvoicePdfStorage.BuildStorageKey(invoice, ownerId)));
    }

    private static LegacyInvoicePdfBackfillService CreateService(
        AppDbContext dbContext,
        IBlobStore blobStore,
        DateTimeOffset now)
    {
        return new LegacyInvoicePdfBackfillService(
            dbContext,
            blobStore,
            new FixedTimeProvider(now),
            NullLogger<LegacyInvoicePdfBackfillService>.Instance);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"legacy-invoice-pdf-backfill-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
