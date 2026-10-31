using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class IntakeGigExtractorTests
{
    private sealed class CaptureLogger : ILogger<IntakeGigExtractor>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
    [Theory]
    [InlineData(20, 90)]
    [InlineData(120, 120)]
    public void ProviderTimeout_UsesGenerousMinimumWithoutShorteningConfiguredTimeout(int configured, int expected) => Assert.Equal(TimeSpan.FromSeconds(expected), IntakeGigExtractor.ProviderTimeout(TimeSpan.FromSeconds(configured)));
    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider { public override DateTimeOffset GetUtcNow() => instant; }
    [Theory]
    [InlineData("%PDF-1.7\nsynthetic", true)]
    [InlineData("not a PDF", false)]
    public void PdfHeaderDetection_RejectsUnreadableUploads(string input, bool expected) => Assert.Equal(expected, VertexIntakeIntentClassifier.HasPdfHeader(System.Text.Encoding.ASCII.GetBytes(input)));

    [Fact]
    public void Parse_MapsMultipleRowsAndPreservesUncertainty()
    {
        var rows = IntakeGigExtractor.Parse("""{"gigs":[{"title":"Show","date":"2026-12-01","client":"Band","venue":"Hall","address":null,"fee":"250.00","currency":"GBP","notes":null,"sourceReference":"page 1","confidence":"High","warnings":[]},{"title":null,"date":null,"client":null,"venue":null,"address":null,"fee":null,"currency":null,"notes":null,"sourceReference":"page 2","confidence":"Low","warnings":["Conflicting dates"]}]}""");
        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateOnly(2026, 12, 1), rows[0].ProposedDate);
        Assert.Equal(250m, rows[0].ProposedFee);
        Assert.Equal(GigImportDraftConfidence.Low, rows[1].Confidence);
        Assert.Null(rows[1].ProposedDate);
        Assert.Contains("Conflicting dates", rows[1].WarningsJson);
    }

    [Fact]
    public void Parse_DoesNotTreatUnknownCurrencyAsGbp()
    {
        var rows = IntakeGigExtractor.Parse("""{"gigs":[{"title":"Show","date":null,"client":null,"venue":null,"address":null,"fee":"200","currency":"USD","notes":null,"sourceReference":"page 1","confidence":"Low","warnings":[]}]}""");
        Assert.Null(rows[0].ProposedFee);
        Assert.Contains("GBP was not confirmed", rows[0].WarningsJson);
    }

    [Fact]
    public async Task PdfExtraction_SendsOriginalBytesAndPreservesLowConfidenceEvidence()
    {
        var bytes = SyntheticScannedPdf();
        var store = new InMemoryExpenseAttachmentStore();
        await store.SaveAsync("source", new MemoryStream(bytes), "application/pdf", TestContext.Current.CancellationToken);
        var options = Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" });
        using var budget = new ReceiptAnalysisBudget(options);
        var engine = new IntakeGigExtractor(store, options, budget, (_, contents, config, _) =>
        {
            Assert.Equal(bytes, contents[0].Parts![0].InlineData!.Data);
            Assert.Equal("application/pdf", contents[0].Parts![0].InlineData!.MimeType);
            Assert.Equal("application/json", config.ResponseMimeType);
            return Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = """{"gigs":[{"title":"Concert","date":null,"client":null,"venue":null,"address":null,"fee":null,"currency":null,"notes":null,"sourceReference":"page 1","confidence":"Low","warnings":["Date unreadable"]}]}""" }] } }] });
        });
        var intake = new CurrentIntake { UserId = Guid.NewGuid(), SourceType = IntakeSourceType.File, StorageKey = "source", ContentType = "application/pdf", SizeBytes = bytes.Length };
        var result = await engine.ExtractAsync(intake, TestContext.Current.CancellationToken);
        Assert.Equal(GigImportDraftConfidence.Low, Assert.Single(result).Confidence);
        Assert.Contains("Date unreadable", result[0].WarningsJson);
        Assert.Contains("not independently verified", result[0].WarningsJson);
    }

    [Fact]
    public async Task TextExtraction_RetainsDraftWithUnverifiedEvidenceWarning()
    {
        var options = Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" });
        using var budget = new ReceiptAnalysisBudget(options);
        var engine = new IntakeGigExtractor(new InMemoryExpenseAttachmentStore(), options, budget, (_, _, _, _) => Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = """{"gigs":[{"title":"Concert","date":null,"client":null,"venue":null,"address":null,"fee":null,"currency":null,"notes":null,"sourceReference":"not in source","confidence":"Low","warnings":[]}]}""" }] } }] }));
        var intake = new CurrentIntake { UserId = Guid.NewGuid(), SourceType = IntakeSourceType.Url, Text = "Actual document contents" };
        var row = Assert.Single(await engine.ExtractAsync(intake, TestContext.Current.CancellationToken));
        Assert.Null(row.SourceReference);
        Assert.Equal(GigImportDraftConfidence.Low, row.Confidence);
        Assert.Contains("Source evidence could not be verified", row.WarningsJson);
    }

    [Fact]
    public async Task MultiGigText_ResolvesSectionIdsAndRetainsUnverifiedRows()
    {
        var options = Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" });
        using var budget = new ReceiptAnalysisBudget(options);
        var source = "First show\n" + new string('x', 390) + "Second show";
        var resultJson = """{"gigs":[{"title":"First","date":null,"client":null,"venue":null,"address":null,"fee":null,"currency":null,"notes":null,"sourceReference":"S0001","confidence":"High","warnings":[]},{"title":"Second","date":null,"client":null,"venue":null,"address":null,"fee":null,"currency":null,"notes":null,"sourceReference":"S0002","confidence":"High","warnings":[]},{"title":"Third","date":null,"client":null,"venue":null,"address":null,"fee":null,"currency":null,"notes":null,"sourceReference":"S9999","confidence":"High","warnings":[]},{"title":"Fourth","date":null,"client":null,"venue":null,"address":null,"fee":null,"currency":null,"notes":null,"sourceReference":null,"confidence":"High","warnings":[]}]}""";
        var engine = new IntakeGigExtractor(new InMemoryExpenseAttachmentStore(), options, budget, (_, contents, _, _) =>
        {
            Assert.Contains("S0001", contents[0].Parts![0].Text);
            Assert.Contains("S0002", contents[0].Parts![0].Text);
            return Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = resultJson }] } }] });
        });
        var rows = await engine.ExtractAsync(new CurrentIntake { UserId = Guid.NewGuid(), SourceType = IntakeSourceType.Url, Text = source }, TestContext.Current.CancellationToken);
        Assert.Equal(4, rows.Count);
        Assert.StartsWith("Section S0001:", rows[0].SourceReference);
        Assert.Contains("Second show", rows[1].SourceReference);
        Assert.Null(rows[2].SourceReference);
        Assert.Equal(GigImportDraftConfidence.Low, rows[2].Confidence);
        Assert.Contains("could not be verified", rows[2].WarningsJson);
        Assert.Null(rows[3].SourceReference);
        Assert.Contains("could not be verified", rows[3].WarningsJson);
    }

    [Fact]
    public async Task TextSections_PreserveSourceAndPreferParagraphBoundaries()
    {
        var options = Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" });
        using var budget = new ReceiptAnalysisBudget(options);
        var source = new string('a', 110) + "\n" + new string('b', 350);
        var engine = new IntakeGigExtractor(new InMemoryExpenseAttachmentStore(), options, budget, (_, contents, _, _) =>
        {
            using var document = System.Text.Json.JsonDocument.Parse(contents[0].Parts![0].Text!);
            var sections = document.RootElement.EnumerateArray().ToArray();
            Assert.EndsWith("\n", sections[0].GetProperty("text").GetString());
            Assert.Equal(source, string.Concat(sections.Select(section => section.GetProperty("text").GetString())));
            return Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = """{"gigs":[]}""" }] } }] });
        });
        Assert.Empty(await engine.ExtractAsync(new CurrentIntake { UserId = Guid.NewGuid(), SourceType = IntakeSourceType.Text, Text = source }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExtractionFailure_LogsSafeCategoryWithoutSourceOrProviderOutput()
    {
        const string privateText = "private booking content";
        var options = Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" });
        using var budget = new ReceiptAnalysisBudget(options);
        var logs = new CaptureLogger();
        var engine = new IntakeGigExtractor(new InMemoryExpenseAttachmentStore(), options, budget, (_, _, _, _) => Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = privateText }] } }] }), null, logs);
        var failure = await Assert.ThrowsAsync<GigPreparationException>(() => engine.ExtractAsync(new CurrentIntake { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), SourceType = IntakeSourceType.Text, Text = privateText }, TestContext.Current.CancellationToken));
        Assert.Equal("invalid_response", failure.Code);
        Assert.Contains(logs.Messages, message => message.Contains("invalid_response", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(privateText, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("2026-12-16", true)]
    [InlineData("2028-12-16", false)]
    [InlineData("2025-12-16", false)]
    public async Task YearlessDate_UsesLondonReferenceAndRejectsDistantGuess(string proposedDate, bool accepted)
    {
        var options = Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" });
        using var budget = new ReceiptAnalysisBudget(options);
        var instant = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        var json = $$"""{"gigs":[{"title":"Piano","date":"{{proposedDate}}","client":null,"venue":null,"address":null,"fee":null,"currency":null,"notes":null,"sourceReference":"16th December","confidence":"High","warnings":[]}]}""";
        var engine = new IntakeGigExtractor(new InMemoryExpenseAttachmentStore(), options, budget, (_, _, config, _) =>
        {
            Assert.Contains("2026-10-31", config.SystemInstruction!.Parts![0].Text);
            return Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = json }] } }] });
        }, new FixedClock(instant));
        var intake = new CurrentIntake { UserId = Guid.NewGuid(), SourceType = IntakeSourceType.Text, Text = "Piano on 16th December" };
        if (accepted)
        {
            var rows = await engine.ExtractAsync(intake, TestContext.Current.CancellationToken);
            Assert.Contains("check the year", Assert.Single(rows).WarningsJson);
        }
        else Assert.Equal("invalid_date", (await Assert.ThrowsAsync<GigPreparationException>(() => engine.ExtractAsync(intake, TestContext.Current.CancellationToken))).Code);
    }

    [Theory]
    [InlineData("this coming Sunday", "2026-11-01", true)]
    [InlineData("next Saturday", "2026-11-07", true)]
    [InlineData("last Saturday", "2026-10-24", true)]
    [InlineData("16 December next year", "2027-12-16", true)]
    [InlineData("16 December 2025", "2025-12-16", false)]
    public async Task RelativeAndExplicitDates_PreserveEvidenceAndShowInferences(string source, string date, bool inferred)
    {
        var options = Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" });
        using var budget = new ReceiptAnalysisBudget(options);
        var json = $$"""{"gigs":[{"title":"Piano","date":"{{date}}","client":null,"venue":null,"address":null,"fee":null,"currency":null,"notes":null,"sourceReference":"{{source}}","confidence":"Medium","warnings":[]}]}""";
        var engine = new IntakeGigExtractor(new InMemoryExpenseAttachmentStore(), options, budget, (_, _, config, _) =>
        {
            Assert.Contains("ambiguous", config.SystemInstruction!.Parts![0].Text);
            return Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = json }] } }] });
        }, new FixedClock(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));
        var row = Assert.Single(await engine.ExtractAsync(new CurrentIntake { UserId = Guid.NewGuid(), SourceType = IntakeSourceType.Text, Text = source }, TestContext.Current.CancellationToken));
        Assert.Equal(DateOnly.Parse(date), row.ProposedDate);
        Assert.Equal(inferred, row.WarningsJson.Contains("check the year", StringComparison.Ordinal));
        if (source == "next Saturday") Assert.Contains("can be ambiguous", row.WarningsJson);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"gigs\":[{}]}")]
    [InlineData("{\"gigs\":[{\"title\":null,\"date\":\"yesterday\",\"client\":null,\"venue\":null,\"address\":null,\"fee\":null,\"notes\":null,\"sourceReference\":\"p1\",\"confidence\":\"High\"}]}")]
    public void Parse_RejectsMalformedResults(string input) => Assert.Throws<InvalidOperationException>(() => IntakeGigExtractor.Parse(input));

    private static byte[] SyntheticScannedPdf()
    {
        var pdf = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        void Object(int id, string body) { offsets.Add(pdf.Length); pdf.Append($"{id} 0 obj\n{body}\nendobj\n"); }
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>");
        const string drawing = "q 100 0 0 100 0 0 cm /Im0 Do Q\n";
        Object(4, $"<< /Length {drawing.Length} >>\nstream\n{drawing}endstream");
        Object(5, "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /ASCIIHexDecode /Length 3 >>\nstream\n00>\nendstream");
        var xref = pdf.Length;
        pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) pdf.Append($"{offset:0000000000} 00000 n \n");
        pdf.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
