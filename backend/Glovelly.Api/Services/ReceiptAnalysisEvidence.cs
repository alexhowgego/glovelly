using System.Text.Json;
using System.Text.Json.Serialization;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public static class ReceiptAnalysisEvidence
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public static string Serialize(ReceiptAnalysis analysis) => JsonSerializer.Serialize(analysis, Options);
    public static ReceiptAnalysis? Read(string json)
    {
        try { return JsonSerializer.Deserialize<ReceiptAnalysis>(json, Options); }
        catch (JsonException) { return null; }
    }
    public static bool HasConfirmedGbp(ReceiptAnalysis analysis) => analysis.Currency == "GBP" && analysis.CurrencyConfidence == ReceiptAnalysisConfidence.High;
}
