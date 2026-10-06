using Google.GenAI.Types;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class IntakeAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_StoresValidatedReceiptEvidenceWithoutCreatingBusinessData()
    {
        var store = await CreateStoreAsync();
        var service = CreateService(store, """{"merchant":"Station Cafe","merchantConfidence":"high","totalAmount":"12.50","totalAmountConfidence":"high","currency":"GBP","currencyConfidence":"high","warnings":[]}""");
        var intake = CreateIntake();

        await service.AnalyzeAsync(intake, TestContext.Current.CancellationToken);

        Assert.Equal(IntakeAnalysisState.Succeeded, intake.AnalysisState);
        Assert.Equal(IntakeIntent.Receipt, intake.Intent);
        Assert.Equal(ReceiptAnalysisConfidence.High, intake.Confidence);
        Assert.Contains("Station Cafe", intake.EvidenceJson, StringComparison.Ordinal);
        var attempt = Assert.Single(intake.AnalysisAttempts);
        Assert.Equal("VertexAi", attempt.Provider);
        Assert.Equal("test-model", attempt.Model);
        Assert.Equal("intake-receipt-v1", attempt.PromptVersion);
    }

    [Fact]
    public async Task AnalyzeAsync_UnsupportedMediaRetainsIntakeWithSafeFailureAndSkipsProvider()
    {
        var called = false;
        var store = await CreateStoreAsync();
        var service = CreateService(store, "{}", () => called = true);
        var intake = CreateIntake();
        intake.ContentType = "image/heic";

        await service.AnalyzeAsync(intake, TestContext.Current.CancellationToken);

        Assert.Equal(IntakeAnalysisState.Failed, intake.AnalysisState);
        Assert.Equal("unsupported_media", intake.FailureCode);
        Assert.Equal(IntakeIntent.Unknown, intake.Intent);
        Assert.Equal("[]", intake.EvidenceJson);
        Assert.False(called);
        Assert.Single(intake.AnalysisAttempts);
    }

    [Fact]
    public async Task AnalyzeAsync_MalformedProviderResponseRetainsSafeFailure()
    {
        var service = CreateService(await CreateStoreAsync(), "not json");
        var intake = CreateIntake();

        await service.AnalyzeAsync(intake, TestContext.Current.CancellationToken);

        Assert.Equal(IntakeAnalysisState.Failed, intake.AnalysisState);
        Assert.Equal("invalid_response", intake.FailureCode);
        Assert.Equal("[]", intake.AnalysisAttempts.Single().EvidenceJson);
    }

    [Fact]
    public async Task AnalyzeAsync_GoogleUrlUsesMetadataOnlyWithoutCallingTheProvider()
    {
        var called = false;
        var service = CreateService(new InMemoryExpenseAttachmentStore(), "{}", () => called = true);
        var intake = new CurrentIntake { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), SourceType = IntakeSourceType.Url, Url = "https://docs.google.com/document/d/example/edit" };

        await service.AnalyzeAsync(intake, TestContext.Current.CancellationToken);

        Assert.False(called);
        Assert.Equal(IntakeIntent.Resource, intake.Intent);
        Assert.Equal("Metadata", intake.AnalysisAttempts.Single().Provider);
    }

    [Fact]
    public async Task AnalyzeAsync_PastedTextIsSentAsTextWithoutLoadingABlob()
    {
        var sawText = false;
        var service = new IntakeAnalysisService(
            (_, contents, _, _) =>
            {
                var parts = contents[0].Parts!;
                sawText = parts[1].InlineData is null && parts[1].Text!.Contains("Train fare £24.50", StringComparison.Ordinal);
                return Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = """{"merchant":"Rail Co","merchantConfidence":"high","warnings":["Check the travel date"]}""" }] } }] });
            }, new InMemoryExpenseAttachmentStore(),
            Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" }),
            TimeProvider.System, NullLogger<IntakeAnalysisService>.Instance);
        var intake = new CurrentIntake { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), SourceType = IntakeSourceType.Text, Text = "Train fare £24.50" };

        await service.AnalyzeAsync(intake, TestContext.Current.CancellationToken);

        Assert.True(sawText);
        Assert.Equal(IntakeIntent.Receipt, intake.Intent);
        Assert.Contains("Check the travel date", intake.AnalysisAttempts.Single().EvidenceJson);
    }

    [Fact]
    public async Task AnalyzeAsync_TimeoutIsSafeAndRetainsSourceForRetry()
    {
        var service = new IntakeAnalysisService(
            async (_, _, _, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new GenerateContentResponse(); },
            await CreateStoreAsync(),
            Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test", Timeout = TimeSpan.FromMilliseconds(20) }),
            TimeProvider.System, NullLogger<IntakeAnalysisService>.Instance);
        var intake = CreateIntake();

        await service.AnalyzeAsync(intake, TestContext.Current.CancellationToken);

        Assert.Equal("timeout", intake.FailureCode);
        Assert.Equal(IntakeAnalysisState.Failed, intake.AnalysisState);
        Assert.Equal("intake", intake.StorageKey);
        Assert.Single(intake.AnalysisAttempts);
    }

    [Fact]
    public async Task AnalyzeAsync_ReanalysisKeepsIndependentAttempts()
    {
        var service = CreateService(await CreateStoreAsync(), """{"merchant":"Rail Co","merchantConfidence":"high","totalAmount":"24.50","totalAmountConfidence":"medium","warnings":[]}""");
        var intake = CreateIntake();

        await service.AnalyzeAsync(intake, TestContext.Current.CancellationToken);
        await service.AnalyzeAsync(intake, TestContext.Current.CancellationToken);

        Assert.Equal(ReceiptAnalysisConfidence.Medium, intake.Confidence);
        Assert.Equal(2, intake.AnalysisAttempts.Count);
        Assert.NotEqual(intake.AnalysisAttempts[0].Id, intake.AnalysisAttempts[1].Id);
    }

    private static CurrentIntake CreateIntake() => new()
    {
        Id = Guid.NewGuid(), UserId = Guid.NewGuid(), SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = "intake",
    };

    private static async Task<InMemoryExpenseAttachmentStore> CreateStoreAsync()
    {
        var store = new InMemoryExpenseAttachmentStore();
        await store.SaveAsync("intake", new MemoryStream(Encoding.UTF8.GetBytes("receipt")), "image/jpeg");
        return store;
    }

    private static IntakeAnalysisService CreateService(InMemoryExpenseAttachmentStore store, string response, Action? called = null) => new(
        (_, _, _, _) =>
        {
            called?.Invoke();
            return Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = response }] } }] });
        },
        store,
        Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test-project", VertexAiLocation = "eu", VertexAiModel = "test-model" }),
        TimeProvider.System,
        NullLogger<IntakeAnalysisService>.Instance);
}
