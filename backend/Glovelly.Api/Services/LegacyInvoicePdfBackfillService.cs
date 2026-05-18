using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class LegacyInvoicePdfBackfillService(
    AppDbContext dbContext,
    IBlobStore blobStore,
    TimeProvider timeProvider,
    ILogger<LegacyInvoicePdfBackfillService> logger)
{
    public async Task BackfillAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var invoiceIds = await dbContext.Invoices
                .Where(invoice => invoice.PdfBlob != null && invoice.PdfStorageKey == null)
                .Select(invoice => invoice.Id)
                .ToListAsync(cancellationToken);

            var found = invoiceIds.Count;
            var migrated = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var invoiceId in invoiceIds)
            {
                try
                {
                    var invoice = await dbContext.Invoices
                        .SingleOrDefaultAsync(invoice => invoice.Id == invoiceId, cancellationToken);

                    if (invoice?.PdfBlob is null || !string.IsNullOrWhiteSpace(invoice.PdfStorageKey))
                    {
                        skipped++;
                        continue;
                    }

                    var key = InvoicePdfStorage.BuildStorageKey(invoice, invoice.CreatedByUserId);
                    var pdfBytes = invoice.PdfBlob;

                    // Temporary startup migration for GitHub issue #118. Keep PdfBlob populated for rollback.
                    await blobStore.SaveAsync(
                        new BlobWriteRequest(
                            key,
                            new MemoryStream(pdfBytes, writable: false),
                            InvoicePdfStorage.ContentType,
                            pdfBytes.Length),
                        cancellationToken);

                    invoice.PdfStorageKey = key;
                    invoice.PdfFileName = string.IsNullOrWhiteSpace(invoice.PdfFileName)
                        ? $"{invoice.InvoiceNumber}.pdf"
                        : invoice.PdfFileName;
                    invoice.PdfContentType = string.IsNullOrWhiteSpace(invoice.PdfContentType)
                        ? InvoicePdfStorage.ContentType
                        : invoice.PdfContentType;
                    invoice.PdfSizeBytes = pdfBytes.Length;
                    invoice.PdfGeneratedAt ??= timeProvider.GetUtcNow();

                    await dbContext.SaveChangesAsync(cancellationToken);
                    migrated++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    dbContext.ChangeTracker.Clear();
                    logger.LogError(
                        ex,
                        "Failed to backfill legacy invoice PDF for invoice {InvoiceId}.",
                        invoiceId);
                }
            }

            logger.LogInformation(
                "Legacy invoice PDF backfill complete. Found: {Found}, migrated: {Migrated}, skipped: {Skipped}, failed: {Failed}.",
                found,
                migrated,
                skipped,
                failed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy invoice PDF backfill failed before invoice processing could complete.");
        }
    }
}
