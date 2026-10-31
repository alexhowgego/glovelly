using System.Text;
using System.Text.Json;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

// Persistence/classification adapter; provider execution belongs to the shared engine.
public sealed class IntakeAnalysisService(ReceiptExtractionEngine engine, TimeProvider clock, IIntakeIntentClassifier? classifier = null, GoogleDocIntakeReader? docs = null, ILogger<IntakeAnalysisService>? logger = null)
{
    public async Task AnalyzeAsync(CurrentIntake intake, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        ReceiptAnalysisConfidence? classificationConfidence = null;
        var documentId = "";
        var isGoogleDoc = intake.SourceType == IntakeSourceType.Url && GoogleDocIntakeReader.TryGetDocumentId(intake.Url, out documentId);
        if (isGoogleDoc && docs is not null)
        {
            try { intake.Text = await docs.ReadAsync(intake.UserId, documentId!, cancellationToken); }
            catch (Exception error) when (!cancellationToken.IsCancellationRequested)
            {
                logger?.LogWarning("Google Docs intake acquisition failed ({FailureCode}, {ErrorType}).", error is GoogleDocReadException diagnostic ? diagnostic.Code : "document_unavailable", error.GetType().Name);
                intake.Text = null;
                intake.IntentEvidenceJson = null;
                intake.EvidenceJson = "[]";
                intake.AnalysisState = IntakeAnalysisState.Failed;
                intake.Intent = IntakeIntent.Unknown;
                intake.Confidence = ReceiptAnalysisConfidence.None;
                intake.FailureCode = error is GoogleDocReadException docError ? docError.Code : "document_unavailable";
                intake.FailureMessage = error is GoogleDocReadException safe ? safe.Message : "Google Docs could not read this document. Check access, retry, or attach the link as a resource.";
                intake.UpdatedAt = clock.GetUtcNow();
                intake.AnalysisAttempts.Add(new IntakeAnalysisAttempt { Id = Guid.NewGuid(), CurrentIntakeId = intake.Id, State = intake.AnalysisState, Intent = intake.Intent, Confidence = intake.Confidence, Provider = "GoogleDocs", PromptVersion = "intake-document-v1", RequestedAt = now, CompletedAt = intake.UpdatedAt, FailureCode = intake.FailureCode, FailureMessage = intake.FailureMessage });
                return;
            }
        }
        if ((intake.SourceType != IntakeSourceType.Url || isGoogleDoc && docs is not null) && classifier is not null)
        {
            IntakeIntentClassification? classification;
            IntakeClassificationException? classificationError = null;
            try { classification = await classifier.ClassifyAsync(intake, cancellationToken); }
            catch (IntakeClassificationException error) { classification = null; classificationError = error; }
            catch (Exception) when (!cancellationToken.IsCancellationRequested) { classification = null; }
            classificationConfidence = classification?.Confidence;
            intake.IntentEvidenceJson = classification is null ? null : JsonSerializer.Serialize(new[] { new { label = "Interpretation", value = classification.Evidence } });
            if (classification is null || intake.SourceType == IntakeSourceType.Url || classification.Intent != IntakeIntent.Receipt || classification.Confidence < ReceiptAnalysisConfidence.Medium)
            {
                intake.EvidenceJson = "[]";
                intake.AnalysisState = classification is null ? IntakeAnalysisState.Failed : IntakeAnalysisState.Succeeded;
                intake.Intent = intake.SourceType == IntakeSourceType.Url && classification?.Intent == IntakeIntent.Receipt ? IntakeIntent.Unknown : classification?.Intent ?? IntakeIntent.Unknown;
                intake.Confidence = classification?.Confidence ?? ReceiptAnalysisConfidence.None;
                intake.FailureCode = classification is null ? classificationError?.Code ?? "unavailable" : null;
                intake.FailureMessage = classification is null ? classificationError?.Message ?? "Analysis could not be completed. Retry or choose how to treat this source." : null;
                intake.UpdatedAt = clock.GetUtcNow();
                intake.AnalysisAttempts.Add(new IntakeAnalysisAttempt { Id = Guid.NewGuid(), CurrentIntakeId = intake.Id, State = intake.AnalysisState, Intent = intake.Intent, Confidence = intake.Confidence, Provider = "VertexAi", PromptVersion = "intake-intent-v1", RequestedAt = now, CompletedAt = intake.UpdatedAt, FailureCode = intake.FailureCode, FailureMessage = intake.FailureMessage, EvidenceJson = intake.IntentEvidenceJson ?? "[]" });
                return;
            }
        }
        var receipt = intake.SourceType == IntakeSourceType.Url
            ? new ReceiptAnalysis { Id = Guid.NewGuid(), Provider = "Metadata", PromptVersion = "intake-url-v1", Status = ReceiptAnalysisStatus.Succeeded, RequestedAt = now, CompletedAt = now }
            : await engine.AnalyzeAsync(new ReceiptSource(intake.StorageKey, intake.ContentType ?? "text/plain", intake.SizeBytes ?? Encoding.UTF8.GetByteCount(intake.Text ?? ""), intake.Text), intake.UserId, cancellationToken);
        var confidence = intake.SourceType == IntakeSourceType.Url ? ReceiptAnalysisConfidence.Low : Confidence(receipt);
        if (classificationConfidence.HasValue && confidence > classificationConfidence.Value) confidence = classificationConfidence.Value;
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
