using System.Text;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

// Persistence/classification adapter; provider execution belongs to the shared engine.
public sealed class IntakeAnalysisService(ReceiptExtractionEngine engine, TimeProvider clock)
{
    public async Task AnalyzeAsync(CurrentIntake intake, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var receipt = intake.SourceType == IntakeSourceType.Url
            ? new ReceiptAnalysis { Id = Guid.NewGuid(), Provider = "Metadata", PromptVersion = "intake-url-v1", Status = ReceiptAnalysisStatus.Succeeded, RequestedAt = now, CompletedAt = now }
            : await engine.AnalyzeAsync(new ReceiptSource(intake.StorageKey, intake.ContentType ?? "text/plain", intake.SizeBytes ?? Encoding.UTF8.GetByteCount(intake.Text ?? ""), intake.Text), intake.UserId, cancellationToken);
        var confidence = intake.SourceType == IntakeSourceType.Url ? ReceiptAnalysisConfidence.Low : Confidence(receipt);
        if (intake.SourceType != IntakeSourceType.Url && receipt.Status == ReceiptAnalysisStatus.Succeeded && confidence == ReceiptAnalysisConfidence.None)
        {
            receipt.Status = ReceiptAnalysisStatus.Failed;
            receipt.FailureCode = "invalid_response";
            receipt.FailureMessage = "Receipt analysis returned no confident receipt fields.";
        }
        var state = receipt.Status == ReceiptAnalysisStatus.Succeeded ? IntakeAnalysisState.Succeeded : IntakeAnalysisState.Failed;
        var intent = state == IntakeAnalysisState.Failed ? IntakeIntent.Unknown : intake.SourceType == IntakeSourceType.Url ? IntakeIntent.Resource : IntakeIntent.Receipt;
        intake.AnalysisState = state;
        intake.Intent = intent;
        intake.Confidence = confidence;
        intake.FailureCode = receipt.FailureCode;
        intake.FailureMessage = receipt.FailureMessage;
        intake.EvidenceJson = ReceiptAnalysisEvidence.Serialize(receipt);
        intake.UpdatedAt = receipt.CompletedAt;
        intake.AnalysisAttempts.Add(new IntakeAnalysisAttempt
        {
            Id = receipt.Id, CurrentIntakeId = intake.Id, State = state, Intent = intent, Confidence = confidence,
            Provider = receipt.Provider, Model = receipt.Model, PromptVersion = receipt.PromptVersion,
            RequestedAt = receipt.RequestedAt, CompletedAt = receipt.CompletedAt,
            FailureCode = receipt.FailureCode, FailureMessage = receipt.FailureMessage, EvidenceJson = intake.EvidenceJson,
        });
    }

    private static ReceiptAnalysisConfidence Confidence(ReceiptAnalysis receipt)
    {
        if (receipt.Status != ReceiptAnalysisStatus.Succeeded) return ReceiptAnalysisConfidence.None;
        var confidences = new[] { receipt.MerchantConfidence, receipt.TransactionDateConfidence, receipt.TotalAmountConfidence, receipt.CurrencyConfidence, receipt.SuggestedCategoryConfidence }.Where(value => value != ReceiptAnalysisConfidence.None).ToList();
        return confidences.Count == 0 ? ReceiptAnalysisConfidence.None : confidences.Min();
    }
}
