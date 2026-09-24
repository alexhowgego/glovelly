using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Data;

internal static class UserFixtureReset
{
    public static async Task ResetAsync(
        AppDbContext db,
        Guid userId,
        IExpenseAttachmentStore attachmentStore,
        IInvoicePdfService invoicePdfService,
        CancellationToken cancellationToken = default)
    {
        var gigIds = await db.Gigs.Where(value => value.CreatedByUserId == userId).Select(value => value.Id).ToListAsync(cancellationToken);
        var invoices = await db.Invoices.Where(value => value.CreatedByUserId == userId).ToListAsync(cancellationToken);
        var expenseIds = await db.GigExpenses.Where(value => gigIds.Contains(value.GigId)).Select(value => value.Id).ToListAsync(cancellationToken);
        var resourceIds = await db.GigExternalResources.Where(value => gigIds.Contains(value.GigId)).Select(value => value.Id).ToListAsync(cancellationToken);
        var importBatchIds = await db.GigImportBatches.Where(value => value.CreatedByUserId == userId).Select(value => value.Id).ToListAsync(cancellationToken);
        var librarySnapshotIds = await db.ForScoreLibrarySnapshots.Where(value => value.CreatedByUserId == userId).Select(value => value.Id).ToListAsync(cancellationToken);
        var setListImportIds = await db.GigSetListImports.Where(value => gigIds.Contains(value.GigId)).Select(value => value.Id).ToListAsync(cancellationToken);
        var expenseAttachments = await db.ExpenseAttachments.Where(value => expenseIds.Contains(value.GigExpenseId)).ToListAsync(cancellationToken);
        var resourceAttachments = await db.GigExternalResourceAttachments.Where(value => resourceIds.Contains(value.GigExternalResourceId)).ToListAsync(cancellationToken);

        foreach (var storageKey in expenseAttachments.Select(value => value.StorageKey).Concat(resourceAttachments.Select(value => value.StorageKey)))
        {
            await attachmentStore.DeleteAsync(storageKey, cancellationToken);
        }

        foreach (var invoice in invoices)
        {
            await invoicePdfService.DeleteAsync(invoice, cancellationToken);
        }

        db.ReceiptAnalyses.RemoveRange(db.ReceiptAnalyses.Where(value => expenseAttachments.Select(attachment => attachment.Id).Contains(value.ExpenseAttachmentId)));
        db.ExpenseAttachments.RemoveRange(expenseAttachments);
        db.GigExternalResourceAttachments.RemoveRange(resourceAttachments);
        db.GigSetListItems.RemoveRange(db.GigSetListItems.Where(value => setListImportIds.Contains(value.GigSetListImportId)));
        db.GigSetListImports.RemoveRange(db.GigSetListImports.Where(value => gigIds.Contains(value.GigId)));
        db.SetListChartMatchJobs.RemoveRange(db.SetListChartMatchJobs.Where(value => value.UserId == userId));
        db.SetListInterpretationJobs.RemoveRange(db.SetListInterpretationJobs.Where(value => value.UserId == userId));
        db.GigCalendarSyncStates.RemoveRange(db.GigCalendarSyncStates.Where(value => value.UserId == userId));
        db.CalendarSyncWorkItems.RemoveRange(db.CalendarSyncWorkItems.Where(value => value.UserId == userId));
        db.GigImportDrafts.RemoveRange(db.GigImportDrafts.Where(value => importBatchIds.Contains(value.BatchId)));
        db.GigImportBatches.RemoveRange(db.GigImportBatches.Where(value => importBatchIds.Contains(value.Id)));
        db.ForScoreCharts.RemoveRange(db.ForScoreCharts.Where(value => librarySnapshotIds.Contains(value.ForScoreLibrarySnapshotId)));
        db.ForScoreLibrarySnapshots.RemoveRange(db.ForScoreLibrarySnapshots.Where(value => librarySnapshotIds.Contains(value.Id)));
        db.McpOAuthAuthorizationCodes.RemoveRange(db.McpOAuthAuthorizationCodes.Where(value => value.UserId == userId));
        db.McpOAuthAccessTokens.RemoveRange(db.McpOAuthAccessTokens.Where(value => value.UserId == userId));
        db.McpOAuthRefreshTokens.RemoveRange(db.McpOAuthRefreshTokens.Where(value => value.UserId == userId));
        db.GoogleDriveIntegrationSettings.RemoveRange(db.GoogleDriveIntegrationSettings.Where(value => value.UserId == userId));
        db.GoogleCalendarIntegrationSettings.RemoveRange(db.GoogleCalendarIntegrationSettings.Where(value => value.UserId == userId));
        db.GoogleConnections.RemoveRange(db.GoogleConnections.Where(value => value.UserId == userId));
        db.InvoiceLines.RemoveRange(db.InvoiceLines.Where(value => invoices.Select(invoice => invoice.Id).Contains(value.InvoiceId)));
        db.GigExpenses.RemoveRange(db.GigExpenses.Where(value => expenseIds.Contains(value.Id)));
        db.GigExternalResources.RemoveRange(db.GigExternalResources.Where(value => resourceIds.Contains(value.Id)));
        db.Invoices.RemoveRange(invoices);
        db.Gigs.RemoveRange(db.Gigs.Where(value => gigIds.Contains(value.Id)));
        db.Clients.RemoveRange(db.Clients.Where(value => value.CreatedByUserId == userId));
        db.SellerProfiles.RemoveRange(db.SellerProfiles.Where(value => value.UserId == userId));

        await db.SaveChangesAsync(cancellationToken);
    }
}
