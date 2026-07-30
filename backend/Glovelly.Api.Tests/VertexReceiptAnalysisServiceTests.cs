using Google.GenAI.Types;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;
using Xunit;
using GenAiType = Google.GenAI.Types.Type;

namespace Glovelly.Api.Tests;

public sealed class VertexReceiptAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_PersistsValidatedSuggestionsAndSendsInlineMedia()
    {
        string? capturedModel = null;
        List<Content>? capturedContents = null;
        GenerateContentConfig? capturedConfig = null;
        await using var db = CreateDb();
        var store = await CreateStoreAsync();
        var service = CreateService(db, store, (model, contents, config) =>
        {
            capturedModel = model;
            capturedContents = contents;
            capturedConfig = config;
            return """{"merchant":"Station Cafe","merchantConfidence":"high","transactionDate":"2026-01-02","totalAmount":"12.50","totalAmountConfidence":"medium","currency":"GBP","suggestedCategory":"meals","warnings":[]}""";
        });

        var result = await service.AnalyzeAsync(CreateAttachment(), TestContext.Current.CancellationToken);

        Assert.Equal(ReceiptAnalysisStatus.Succeeded, result.Status);
        Assert.Equal("Station Cafe", result.Merchant.Value);
        Assert.Equal(12.50m, result.TotalAmount.Value);
        Assert.Equal(ReceiptAnalysisConfidence.High, result.Merchant.Confidence);
        Assert.Equal("test-model", capturedModel);
        Assert.NotNull(capturedConfig);
        Assert.Equal("application/json", capturedConfig!.ResponseMimeType);
        Assert.Equal(GenAiType.Object, capturedConfig.ResponseSchema!.Type);
        Assert.Equal(GenAiType.String, capturedConfig.ResponseSchema.Properties!["merchantConfidence"].Type);
        Assert.False(capturedConfig.ResponseSchema.Properties["merchant"].Nullable);
        Assert.NotNull(capturedContents);
        Assert.Equal("image/jpeg", capturedContents![0].Parts![1].InlineData!.MimeType);
        var attempt = Assert.Single(db.ReceiptAnalyses);
        Assert.Equal("VertexAi", attempt.Provider);
        Assert.Equal("receipt-v1", attempt.PromptVersion);
    }

    [Fact]
    public async Task AnalyzeAsync_RetainsValidFieldsAndWarnsForInvalidOnes()
    {
        await using var db = CreateDb();
        var service = CreateService(db, await CreateStoreAsync(), (_, _, _) =>
            """{"merchant":"Station Cafe","merchantConfidence":"high","transactionDate":"02/01/2026","totalAmount":12.50,"currency":"gbp","suggestedCategory":"unknown","warnings":["Check total"]}""");

        var result = await service.AnalyzeAsync(CreateAttachment(), TestContext.Current.CancellationToken);

        Assert.Equal(ReceiptAnalysisStatus.Succeeded, result.Status);
        Assert.Equal("Station Cafe", result.Merchant.Value);
        Assert.Null(result.TransactionDate.Value);
        Assert.Null(result.TotalAmount.Value);
        Assert.Contains("transactionDate was ignored because it was invalid.", result.Warnings);
        Assert.Contains("Check total", result.Warnings);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotCallVertexForUnsupportedMediaAndPersistsSafeFailure()
    {
        await using var db = CreateDb();
        var called = false;
        var service = CreateService(db, await CreateStoreAsync(), (_, _, _) => { called = true; return "{}"; });
        var attachment = CreateAttachment();
        attachment.ContentType = "image/heic";

        var result = await service.AnalyzeAsync(attachment, TestContext.Current.CancellationToken);

        Assert.Equal(ReceiptAnalysisStatus.Failed, result.Status);
        Assert.Equal("unsupported_media", result.FailureCode);
        Assert.False(called);
        Assert.Null(result.Merchant.Value);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<InMemoryExpenseAttachmentStore> CreateStoreAsync()
    {
        var store = new InMemoryExpenseAttachmentStore();
        await store.SaveAsync("receipt", new MemoryStream(Encoding.UTF8.GetBytes("receipt")), "image/jpeg");
        return store;
    }

    private static ExpenseAttachment CreateAttachment() => new()
    {
        Id = Guid.NewGuid(), GigExpenseId = Guid.NewGuid(), StorageKey = "receipt", ContentType = "image/jpeg", SizeBytes = 7,
    };

    private static VertexReceiptAnalysisService CreateService(AppDbContext db, IExpenseAttachmentStore store, Func<string, List<Content>, GenerateContentConfig, string> response)
    {
        return new VertexReceiptAnalysisService(
            (model, contents, config, _) => Task.FromResult(new GenerateContentResponse
            {
                Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = response(model, contents, config) }] } }],
            }),
            db,
            store,
            Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test-project", VertexAiLocation = "eu", VertexAiModel = "test-model" }),
            TimeProvider.System,
            NullLogger<VertexReceiptAnalysisService>.Instance);
    }
}
