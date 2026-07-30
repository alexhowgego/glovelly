using Google.GenAI;
using Google.GenAI.Types;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using GenAiClient = Google.GenAI.Client;
using GenAiType = Google.GenAI.Types.Type;

namespace Glovelly.Api.Services;

public sealed class VertexReceiptAnalysisService : IReceiptAnalysisService
{
    private const string Provider = "VertexAi";
    private const string PromptVersion = "receipt-v1";
    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal) { "travel", "meals", "accommodation", "equipment", "other" };
    private readonly Func<string, List<Content>, GenerateContentConfig, CancellationToken, Task<GenerateContentResponse>> _generateContentAsync;
    private readonly AppDbContext _db;
    private readonly IExpenseAttachmentStore _attachmentStore;
    private readonly ReceiptAnalysisSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<VertexReceiptAnalysisService> _logger;

    public VertexReceiptAnalysisService(AppDbContext db, IExpenseAttachmentStore attachmentStore, IOptions<ReceiptAnalysisSettings> options, TimeProvider timeProvider, ILogger<VertexReceiptAnalysisService> logger)
        : this(CreateGenerateContentAsync(options.Value), db, attachmentStore, options, timeProvider, logger) { }

    public VertexReceiptAnalysisService(Func<string, List<Content>, GenerateContentConfig, CancellationToken, Task<GenerateContentResponse>> generateContentAsync, AppDbContext db, IExpenseAttachmentStore attachmentStore, IOptions<ReceiptAnalysisSettings> options, TimeProvider timeProvider, ILogger<VertexReceiptAnalysisService> logger)
    {
        _generateContentAsync = generateContentAsync;
        _db = db;
        _attachmentStore = attachmentStore;
        _settings = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ReceiptAnalysisResult> AnalyzeAsync(ExpenseAttachment attachment, CancellationToken cancellationToken = default)
    {
        var requestedAt = _timeProvider.GetUtcNow();
        var analysis = NewAnalysis(attachment.Id, requestedAt);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!_settings.IsConfigured)
                return await SaveFailureAsync(analysis, "unavailable", "Receipt analysis is currently unavailable.", stopwatch);
            if (!_settings.AllowedContentTypes.Contains(attachment.ContentType, StringComparer.OrdinalIgnoreCase))
                return await SaveFailureAsync(analysis, "unsupported_media", "This receipt type cannot be analysed.", stopwatch);
            if (attachment.SizeBytes <= 0 || attachment.SizeBytes > _settings.MaxFileSizeBytes)
                return await SaveFailureAsync(analysis, "size_limit", "This receipt is too large to analyse.", stopwatch);

            var stored = await _attachmentStore.OpenReadAsync(attachment.StorageKey, cancellationToken);
            using var source = stored.Content;
            using var content = new MemoryStream();
            await source.CopyToAsync(content, cancellationToken);
            if (content.Length == 0 || content.Length > _settings.MaxFileSizeBytes)
                return await SaveFailureAsync(analysis, "missing_content", "The receipt content is unavailable.", stopwatch);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_settings.Timeout);
            var request = BuildRequest(attachment.ContentType, content.ToArray());
            var response = await _generateContentAsync(_settings.VertexAiModel!, request.Contents, request.Config, timeout.Token);
            var text = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (!TryParse(text, analysis, out var invalidResponse))
                return await SaveFailureAsync(analysis, "invalid_response", invalidResponse, stopwatch);

            analysis.Status = ReceiptAnalysisStatus.Succeeded;
            analysis.CompletedAt = _timeProvider.GetUtcNow();
            _db.ReceiptAnalyses.Add(analysis);
            await _db.SaveChangesAsync(cancellationToken);
            LogCompletion(analysis, stopwatch.ElapsedMilliseconds);
            return ToResult(analysis);
        }
        catch (FileNotFoundException)
        {
            return await SaveFailureAsync(analysis, "missing_content", "The receipt content is unavailable.", stopwatch);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await SaveFailureAsync(analysis, "timeout", "Receipt analysis timed out. Please try again.", stopwatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receipt analysis failed for attachment {AttachmentId}.", attachment.Id);
            return await SaveFailureAsync(analysis, "provider_failure", "Receipt analysis could not be completed. Please try again.", stopwatch);
        }
    }

    private static Func<string, List<Content>, GenerateContentConfig, CancellationToken, Task<GenerateContentResponse>> CreateGenerateContentAsync(ReceiptAnalysisSettings settings)
    {
        if (!settings.IsConfigured)
            return (_, _, _, _) => throw new InvalidOperationException("Receipt analysis is not configured.");

        var client = new GenAiClient(vertexAI: true, project: settings.VertexAiProjectId, location: settings.VertexAiLocation, enterprise: true);
        return (model, contents, config, cancellationToken) => client.Models.GenerateContentAsync(model, contents, config, cancellationToken);
    }

    private static (List<Content> Contents, GenerateContentConfig Config) BuildRequest(string contentType, byte[] content) =>
        ([new Content
        {
            Role = "user",
            Parts = [new Part { Text = Prompt }, new Part { InlineData = new Blob { MimeType = contentType, Data = content } }],
        }],
        new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseSchema = ResponseSchema,
        });

    private async Task<ReceiptAnalysisResult> SaveFailureAsync(ReceiptAnalysis analysis, string code, string message, Stopwatch stopwatch)
    {
        analysis.Status = ReceiptAnalysisStatus.Failed;
        analysis.FailureCode = code;
        analysis.FailureMessage = message;
        analysis.CompletedAt = _timeProvider.GetUtcNow();
        _db.ReceiptAnalyses.Add(analysis);
        await _db.SaveChangesAsync();
        LogCompletion(analysis, stopwatch.ElapsedMilliseconds);
        return ToResult(analysis);
    }

    private ReceiptAnalysis NewAnalysis(Guid attachmentId, DateTimeOffset requestedAt) => new()
    {
        Id = Guid.NewGuid(), ExpenseAttachmentId = attachmentId, Provider = Provider,
        Model = _settings.VertexAiModel ?? "", PromptVersion = PromptVersion, RequestedAt = requestedAt,
    };

    private void LogCompletion(ReceiptAnalysis analysis, long elapsedMilliseconds) => _logger.LogInformation(
        "Receipt analysis completed for attachment {AttachmentId} using {Model}: {Status}, {FailureCode}, {ElapsedMilliseconds} ms.",
        analysis.ExpenseAttachmentId, analysis.Model, analysis.Status, analysis.FailureCode, elapsedMilliseconds);

    private static bool TryParse(string? text, ReceiptAnalysis analysis, out string failureMessage)
    {
        failureMessage = "Receipt analysis returned an invalid response.";
        if (string.IsNullOrWhiteSpace(text) || text.Length > 16_384) return false;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var root = document.RootElement;
            var validFields = 0;
            ApplyString(root, "merchant", 200, value => analysis.Merchant = value, value => analysis.MerchantConfidence = value, analysis, ref validFields);
            ApplyDate(root, analysis, ref validFields);
            ApplyTotal(root, analysis, ref validFields);
            ApplyCurrency(root, analysis, ref validFields);
            ApplyCategory(root, analysis, ref validFields);
            ApplyWarnings(root, analysis);
            if (validFields == 0 && analysis.Warnings.Count == 0) return false;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static void ApplyString(JsonElement root, string name, int maxLength, Action<string> setValue, Action<ReceiptAnalysisConfidence> setConfidence, ReceiptAnalysis analysis, ref int validFields)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return;
        if (property.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(property.GetString())) return;
        if (property.ValueKind != JsonValueKind.String || property.GetString() is not { } value || value.Length > maxLength) { analysis.Warnings.Add($"{name} was ignored because it was invalid."); return; }
        setValue(value.Trim()); setConfidence(ReadConfidence(root, name)); validFields++;
    }
    private static void ApplyDate(JsonElement root, ReceiptAnalysis analysis, ref int validFields)
    {
        if (!root.TryGetProperty("transactionDate", out var property) || property.ValueKind == JsonValueKind.Null) return;
        if (property.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(property.GetString())) return;
        if (property.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(property.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) { analysis.TransactionDate = value; analysis.TransactionDateConfidence = ReadConfidence(root, "transactionDate"); validFields++; } else analysis.Warnings.Add("transactionDate was ignored because it was invalid.");
    }
    private static void ApplyTotal(JsonElement root, ReceiptAnalysis analysis, ref int validFields)
    {
        if (!root.TryGetProperty("totalAmount", out var property) || property.ValueKind == JsonValueKind.Null) return;
        if (property.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(property.GetString())) return;
        if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0 && value <= 1_000_000m) { analysis.TotalAmount = value; analysis.TotalAmountConfidence = ReadConfidence(root, "totalAmount"); validFields++; } else analysis.Warnings.Add("totalAmount was ignored because it was invalid.");
    }
    private static void ApplyCurrency(JsonElement root, ReceiptAnalysis analysis, ref int validFields)
    {
        if (!root.TryGetProperty("currency", out var property) || property.ValueKind == JsonValueKind.Null) return;
        var value = property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value is { Length: 3 } && value.All(char.IsAsciiLetter) && value == value.ToUpperInvariant()) { analysis.Currency = value; analysis.CurrencyConfidence = ReadConfidence(root, "currency"); validFields++; } else analysis.Warnings.Add("currency was ignored because it was invalid.");
    }
    private static void ApplyCategory(JsonElement root, ReceiptAnalysis analysis, ref int validFields)
    {
        if (!root.TryGetProperty("suggestedCategory", out var property) || property.ValueKind == JsonValueKind.Null) return;
        var value = property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value is not null && Categories.Contains(value)) { analysis.SuggestedCategory = value; analysis.SuggestedCategoryConfidence = ReadConfidence(root, "suggestedCategory"); validFields++; } else analysis.Warnings.Add("suggestedCategory was ignored because it was invalid.");
    }
    private static void ApplyWarnings(JsonElement root, ReceiptAnalysis analysis)
    {
        if (!root.TryGetProperty("warnings", out var property) || property.ValueKind == JsonValueKind.Null) return;
        if (property.ValueKind != JsonValueKind.Array) { analysis.Warnings.Add("warnings was ignored because it was invalid."); return; }
        foreach (var warning in property.EnumerateArray().Take(10)) if (warning.ValueKind == JsonValueKind.String && warning.GetString() is { Length: > 0 and <= 300 } value) analysis.Warnings.Add(value);
    }
    private static ReceiptAnalysisConfidence ReadConfidence(JsonElement root, string name)
    {
        if (!root.TryGetProperty($"{name}Confidence", out var confidence) || confidence.ValueKind != JsonValueKind.String) return ReceiptAnalysisConfidence.None;
        return confidence.GetString()?.ToLowerInvariant() switch { "low" => ReceiptAnalysisConfidence.Low, "medium" => ReceiptAnalysisConfidence.Medium, "high" => ReceiptAnalysisConfidence.High, _ => ReceiptAnalysisConfidence.None };
    }
    public static ReceiptAnalysisResult ToResult(ReceiptAnalysis value) => new(value.Id, value.Status, value.Provider, value.Model, value.PromptVersion, value.RequestedAt, value.CompletedAt, new ReceiptAnalysisField<string?>(value.Merchant, value.MerchantConfidence), new ReceiptAnalysisField<DateOnly?>(value.TransactionDate, value.TransactionDateConfidence), new ReceiptAnalysisField<decimal?>(value.TotalAmount, value.TotalAmountConfidence), new ReceiptAnalysisField<string?>(value.Currency, value.CurrencyConfidence), new ReceiptAnalysisField<string?>(value.SuggestedCategory, value.SuggestedCategoryConfidence), value.Warnings, value.FailureCode, value.FailureMessage);

    private const string Prompt = "Extract receipt suggestions. Return only a JSON object with merchant, merchantConfidence, transactionDate (YYYY-MM-DD), transactionDateConfidence, totalAmount (invariant decimal string), totalAmountConfidence, currency (uppercase ISO 4217), currencyConfidence, suggestedCategory (travel, meals, accommodation, equipment, or other), suggestedCategoryConfidence, and warnings array. Confidence values must be high, medium, low, or none. Omit unavailable values or return an empty string. Do not infer unavailable values.";

    private static readonly Schema StringSchema = new()
    {
        Type = GenAiType.String,
        Nullable = false,
    };

    private static readonly Schema ResponseSchema = new()
    {
        Type = GenAiType.Object,
        Nullable = false,
        Properties = new Dictionary<string, Schema>
        {
            ["merchant"] = StringSchema,
            ["merchantConfidence"] = StringSchema,
            ["transactionDate"] = StringSchema,
            ["transactionDateConfidence"] = StringSchema,
            ["totalAmount"] = StringSchema,
            ["totalAmountConfidence"] = StringSchema,
            ["currency"] = StringSchema,
            ["currencyConfidence"] = StringSchema,
            ["suggestedCategory"] = StringSchema,
            ["suggestedCategoryConfidence"] = StringSchema,
            ["warnings"] = new Schema
            {
                Type = GenAiType.Array,
                Nullable = false,
                Items = StringSchema,
            },
        },
    };
}
