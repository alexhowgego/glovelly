using Google.GenAI.Types;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;
using GenAiType = Google.GenAI.Types.Type;

namespace Glovelly.Api.Tests;

public sealed class VertexAiSetListChartContextualRankerTests
{
    private static readonly DeterministicSetListChartContextualRanker Fallback = new();
    private static readonly SetListChartRankingSettings Settings = new()
    {
        Provider = "VertexAi",
        VertexAiProjectId = "test-project",
        VertexAiLocation = "eu",
        VertexAiModel = "gemini-3.1-flash-lite",
    };

    private static readonly Guid ChartId = Guid.NewGuid();
    private static readonly Guid ChartId2 = Guid.NewGuid();

    private static readonly SetListChartRankingRequest SingleRowRequest = new(
        Guid.NewGuid(),
        [
            new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "Test Song", "17", "C"),
        ],
        [
            new SetListChartCandidateSet(
                new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "Test Song", "17", "C"),
                [
                    new SetListChartMatchCandidate(
                        new ForScoreChartReference(ChartId, Guid.NewGuid(), "Test Chart", "B-017 Test.pdf", "test chart"),
                        100,
                        "Exact chart number.",
                        ["exact_chart_number"]),
                    new SetListChartMatchCandidate(
                        new ForScoreChartReference(ChartId2, Guid.NewGuid(), "Other Chart", "Other.pdf", "other chart"),
                        40,
                        "Partial title similarity.",
                        ["partial_title_similarity"]),
                ]),
        ]);

    private static readonly IOptions<SetListChartRankingSettings> SettingsOptions = CreateOptions();

    private static IOptions<SetListChartRankingSettings> CreateOptions() => Options.Create(Settings);

    [Fact]
    public async Task RankAsync_ReturnsValidDecisionsFromJsonResponse()
    {
        var decisions = new[]
        {
            new { RowNumber = 1, SelectedChartId = ChartId.ToString(), Status = "suggested", Confidence = "high", Reason = "Chart number matches." },
        };
        var json = JsonSerializer.Serialize(decisions, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var generateContentAsync = CreateGenerateContentAsync(json);

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.Suggested, decision.Status);
        Assert.Equal(ForScoreMappingConfidence.High, decision.Confidence);
        Assert.Equal(ChartId, decision.SelectedChartId);
        Assert.Equal(1, decision.SourceRowNumber);
    }

    [Fact]
    public async Task RankAsync_SendsJsonOnlyRequestToConfiguredModel()
    {
        string? capturedModel = null;
        GenerateContentConfig? capturedConfig = null;
        var ranker = new VertexAiSetListChartContextualRanker(
            (model, _, config, _) =>
            {
                capturedModel = model;
                capturedConfig = config;
                return Task.FromResult(new GenerateContentResponse
                {
                    Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = "{\"decisions\":[]}" }] } }],
                });
            },
            SettingsOptions,
            Fallback,
            NullLogger<VertexAiSetListChartContextualRanker>.Instance);

        await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        Assert.Equal("gemini-3.1-flash-lite", capturedModel);
        Assert.NotNull(capturedConfig);
        Assert.Equal("application/json", capturedConfig!.ResponseMimeType);
        var decisionSchema = capturedConfig.ResponseSchema!.Properties!["decisions"].Items!;
        Assert.Equal(GenAiType.String, decisionSchema.Properties!["selectedChartId"].Type);
        Assert.All(decisionSchema.Properties.Values, schema => Assert.False(schema.Nullable));
    }

    [Fact]
    public async Task RankAsync_FallsBackToDeterministicOnEmptyResponse()
    {
        var generateContentAsync = CreateGenerateContentAsync("");

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.Suggested, decision.Status);
        Assert.Equal(ForScoreMappingConfidence.High, decision.Confidence);
        Assert.Equal(ChartId, decision.SelectedChartId);
    }

    [Fact]
    public async Task RankAsync_FallsBackToDeterministicOnMalformedJson()
    {
        var generateContentAsync = CreateGenerateContentAsync("not valid json at all");

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.Suggested, decision.Status);
        Assert.Equal(ChartId, decision.SelectedChartId);
    }

    [Fact]
    public async Task RankAsync_MarksRowForReviewOnUnknownChartId()
    {
        var decisions = new[]
        {
            new { RowNumber = 1, SelectedChartId = Guid.NewGuid().ToString(), Status = "suggested", Confidence = "high", Reason = "Unknown chart." },
        };
        var json = JsonSerializer.Serialize(decisions, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var generateContentAsync = CreateGenerateContentAsync(json);

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.NeedsReview, decision.Status);
        Assert.Equal(ForScoreMappingConfidence.Low, decision.Confidence);
        Assert.Null(decision.SelectedChartId);
    }

    [Fact]
    public async Task RankAsync_FallsBackToDeterministicOnMissingRowNumber()
    {
        var decisions = new[]
        {
            new { RowNumber = 99, SelectedChartId = ChartId.ToString(), Status = "suggested", Confidence = "high", Reason = "Wrong row." },
        };
        var json = JsonSerializer.Serialize(decisions, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var generateContentAsync = CreateGenerateContentAsync(json);

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.Suggested, decision.Status);
        Assert.Equal(ChartId, decision.SelectedChartId);
    }

    [Fact]
    public async Task RankAsync_ParsesResponseWithMarkdownCodeBlock()
    {
        var decisions = new[]
        {
            new { RowNumber = 1, SelectedChartId = ChartId.ToString(), Status = "suggested", Confidence = "medium", Reason = "OK." },
        };
        var innerJson = JsonSerializer.Serialize(decisions, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var markdownWrapped = $"```json\n{innerJson}\n```";
        var generateContentAsync = CreateGenerateContentAsync(markdownWrapped);

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.Suggested, decision.Status);
        Assert.Equal(ForScoreMappingConfidence.Medium, decision.Confidence);
        Assert.Equal(ChartId, decision.SelectedChartId);
    }

    [Fact]
    public async Task RankAsync_ParsesResponseWrappedInObject()
    {
        var decisions = new[]
        {
            new { RowNumber = 1, SelectedChartId = ChartId.ToString(), Status = "suggested", Confidence = "medium", Reason = "OK." },
        };
        var wrappedJson = JsonSerializer.Serialize(new { Decisions = decisions }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var generateContentAsync = CreateGenerateContentAsync(wrappedJson);

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.Suggested, decision.Status);
        Assert.Equal(ForScoreMappingConfidence.Medium, decision.Confidence);
        Assert.Equal(ChartId, decision.SelectedChartId);
    }

    [Fact]
    public async Task RankAsync_ParsesResponseWithLeadingText()
    {
        var decisions = new[]
        {
            new { RowNumber = 1, SelectedChartId = ChartId.ToString(), Status = "suggested", Confidence = "medium", Reason = "OK." },
        };
        var innerJson = JsonSerializer.Serialize(decisions, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var generateContentAsync = CreateGenerateContentAsync($"Here is the ranking:\n{innerJson}");

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.Suggested, decision.Status);
        Assert.Equal(ForScoreMappingConfidence.Medium, decision.Confidence);
        Assert.Equal(ChartId, decision.SelectedChartId);
    }

    [Fact]
    public async Task RankAsync_NormalizesStatusAndConfidenceVariants()
    {
        var decisions = new[]
        {
            new { RowNumber = 1, SelectedChartId = ChartId.ToString(), Status = "needsreview", Confidence = "LOW", Reason = "" },
        };
        var json = JsonSerializer.Serialize(decisions, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var generateContentAsync = CreateGenerateContentAsync(json);

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.NeedsReview, decision.Status);
        Assert.Equal(ForScoreMappingConfidence.Low, decision.Confidence);
        Assert.Equal(ChartId, decision.SelectedChartId);
        Assert.Equal("Suggested by contextual analysis.", decision.Reason);
    }

    [Fact]
    public async Task RankAsync_FallsBackToDeterministicOnException()
    {
        var generateContentAsync = CreateGenerateContentAsync(new InvalidOperationException("API unavailable"));

        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(SingleRowRequest, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.Suggested, decision.Status);
        Assert.Equal(ChartId, decision.SelectedChartId);
    }

    [Fact]
    public async Task RankAsync_ReturnsEmptyForNoSongRows()
    {
        var request = new SetListChartRankingRequest(
            Guid.NewGuid(),
            [
                new SetListChartMatchInput(null, 1, GigSetListItemKind.Separator, false, "---"),
            ],
            [
                new SetListChartCandidateSet(
                    new SetListChartMatchInput(null, 1, GigSetListItemKind.Separator, false, "---"),
                    []),
            ]);

        var generateContentAsync = CreateGenerateContentAsync("will not be called");
        var ranker = new VertexAiSetListChartContextualRanker(generateContentAsync, SettingsOptions, Fallback, NullLogger<VertexAiSetListChartContextualRanker>.Instance);
        var result = await ranker.RankAsync(request, TestContext.Current.CancellationToken);

        var decision = Assert.Single(result);
        Assert.Equal(ForScoreMappingStatus.NotApplicable, decision.Status);
        Assert.Null(decision.SelectedChartId);
    }

    private static Func<string, List<Content>, GenerateContentConfig, CancellationToken, Task<GenerateContentResponse>> CreateGenerateContentAsync(string responseText)
    {
        return (_, _, _, _) =>
        {
            return Task.FromResult(new GenerateContentResponse
            {
                Candidates = [new Candidate
                {
                    Content = new Content { Parts = [new Part { Text = responseText }] }
                }]
            });
        };
    }

    private static Func<string, List<Content>, GenerateContentConfig, CancellationToken, Task<GenerateContentResponse>> CreateGenerateContentAsync(Exception exception)
    {
        return (_, _, _, _) => throw exception;
    }
}
