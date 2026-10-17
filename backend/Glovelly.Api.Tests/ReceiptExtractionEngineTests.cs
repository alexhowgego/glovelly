using Google.GenAI.Types;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class ReceiptExtractionEngineTests
{
    [Fact]
    public async Task IntakeAndSavedAnalysis_ShareProviderBudgetWhileUrlMetadataDoesNotConsumeIt()
    {
        var userId = Guid.NewGuid();
        var store = new InMemoryExpenseAttachmentStore();
        await store.SaveAsync("receipt", new MemoryStream("receipt"u8.ToArray()), "image/jpeg", TestContext.Current.CancellationToken);
        var options = Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test", PerUserPermitLimit = 1 });
        using var budget = new ReceiptAnalysisBudget(options);
        var calls = 0;
        var engine = new ReceiptExtractionEngine((_, _, _, _) =>
        {
            calls++;
            return Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = """{"merchant":"Cafe","merchantConfidence":"high","warnings":[]}""" }] } }] });
        }, store, options, TimeProvider.System, NullLogger<ReceiptExtractionEngine>.Instance, budget);
        var intakeAdapter = new IntakeAnalysisService(engine, TimeProvider.System);
        await intakeAdapter.AnalyzeAsync(new CurrentIntake { Id = Guid.NewGuid(), UserId = userId, SourceType = IntakeSourceType.Url, Url = "https://example.test" }, TestContext.Current.CancellationToken);
        var intake = new CurrentIntake { Id = Guid.NewGuid(), UserId = userId, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", StorageKey = "receipt", ContentType = "image/jpeg", SizeBytes = 7 };
        await intakeAdapter.AnalyzeAsync(intake, TestContext.Current.CancellationToken);
        Assert.Equal(IntakeAnalysisState.Succeeded, intake.AnalysisState);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var saved = await new AttachmentReceiptAnalysisService(db, engine).AnalyzeAsync(new ExpenseAttachment { Id = Guid.NewGuid(), StorageKey = "receipt", ContentType = "image/jpeg", SizeBytes = 7 }, TestContext.Current.CancellationToken, userId);
        Assert.Equal("rate_limited", saved.FailureCode);
        Assert.Equal(1, calls);
    }
}
