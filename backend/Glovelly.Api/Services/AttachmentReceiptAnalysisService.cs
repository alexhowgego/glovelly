using Glovelly.Api.Data;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed class AttachmentReceiptAnalysisService(AppDbContext db, ReceiptExtractionEngine engine) : IReceiptAnalysisService
{
    public async Task<ReceiptAnalysisResult> AnalyzeAsync(ExpenseAttachment attachment, CancellationToken cancellationToken = default, Guid? userId = null)
    {
        var analysis = await engine.AnalyzeAsync(new ReceiptSource(attachment.StorageKey, attachment.ContentType, attachment.SizeBytes), userId ?? Guid.Empty, cancellationToken);
        analysis.ExpenseAttachmentId = attachment.Id;
        db.ReceiptAnalyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);
        return ReceiptAnalysisResult.FromAnalysis(analysis);
    }

}
