using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Google.GenAI.Types;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class IntakeEndpointsTests : IDisposable
{
    private sealed class FakeGigExtractor : IIntakeGigExtractor
    {
        public Task<IReadOnlyList<GigImportDraft>> ExtractAsync(CurrentIntake intake, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GigImportDraft>>([
                new GigImportDraft { Id = Guid.NewGuid(), ProposedTitle = "First show", ProposedDate = new DateOnly(2027, 1, 1), SourceReference = "First show", Confidence = GigImportDraftConfidence.High },
                new GigImportDraft { Id = Guid.NewGuid(), ProposedTitle = "Second show", SourceReference = "Second show", Confidence = GigImportDraftConfidence.Low },
            ]);
    }
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public IntakeEndpointsTests()
    {
        _factory = new GlovellyApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose() { _client.Dispose(); _factory.Dispose(); }

    private static string ReceiptFingerprint => IntakeSourceIdentity.File("receipt.jpg", "image/jpeg", "receipt"u8.ToArray());
    private static string ReceiptEvidence(string? currency = "GBP", ReceiptAnalysisConfidence currencyConfidence = ReceiptAnalysisConfidence.High) => ReceiptAnalysisEvidence.Serialize(new ReceiptAnalysis
    {
        Id = Guid.NewGuid(), Status = ReceiptAnalysisStatus.Succeeded, Provider = "VertexAi", Model = "test", PromptVersion = "receipt-v1",
        Merchant = "Rail Co", MerchantConfidence = ReceiptAnalysisConfidence.High, TotalAmount = 24.50m, TotalAmountConfidence = ReceiptAnalysisConfidence.High,
        Currency = currency, CurrencyConfidence = currencyConfidence, SuggestedCategory = "travel", SuggestedCategoryConfidence = ReceiptAnalysisConfidence.High,
    });
    private static async Task<HttpResponseMessage> ReplayReceiptAsync(HttpClient client, Guid id)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("receipt"u8.ToArray()); file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "receipt.jpg"); form.Add(new StringContent(id.ToString()), "intakeId");
        return await client.PostAsync("/intake/current/file", form, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NonReceiptOverride_PreventsAutomaticReceiptApplication()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(value => value.Id == TestAuthContext.UserId, TestContext.Current.CancellationToken);
        user.AutomaticReceiptMatching = AutomaticReceiptMatching.HighConfidence;
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        db.Gigs.Add(new Gig { Id = Guid.NewGuid(), ClientId = TestData.RiversideId, Title = "Nearby gig", Date = DateOnly.FromDateTime(clock.GetLocalNow().DateTime), Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId });
        var intake = new CurrentIntake
        {
            Id = Guid.NewGuid(), UserId = TestAuthContext.UserId, SourceFingerprint = ReceiptFingerprint,
            SourceType = IntakeSourceType.Text, Text = "Booking details", AnalysisState = IntakeAnalysisState.Succeeded,
            Intent = IntakeIntent.Receipt, IntentOverride = IntakeIntent.Gigs, Confidence = ReceiptAnalysisConfidence.High,
            EvidenceJson = ReceiptEvidence(),
        };
        db.CurrentIntakes.Add(intake);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await scope.ServiceProvider.GetRequiredService<IntakeWorkflowService>().CompleteAsync(intake, TestContext.Current.CancellationToken);

        Assert.Empty(await db.GigExpenses.ToListAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(await db.CurrentIntakes.FindAsync([intake.Id], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ManualGigDraft_RetainsSourceAndRecoversWithoutCreatingBusinessData()
    {
        var id = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CurrentIntakes.Add(new CurrentIntake { Id = id, UserId = TestAuthContext.UserId, SourceFingerprint = "fingerprint", SourceType = IntakeSourceType.Text, Text = "Booking details", Intent = IntakeIntent.Unknown, AnalysisState = IntakeAnalysisState.Failed });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var first = await _client.PostAsJsonAsync("/intake/current/gig-drafts/manual", new { intakeId = id }, TestContext.Current.CancellationToken);
        first.EnsureSuccessStatusCode();
        var payload = await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        var batchId = payload.GetProperty("batchId").GetGuid();
        var second = await _client.PostAsJsonAsync("/intake/current/gig-drafts/manual", new { intakeId = id }, TestContext.Current.CancellationToken);
        second.EnsureSuccessStatusCode();
        Assert.Equal(batchId, (await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken)).GetProperty("batchId").GetGuid());
        Assert.Equal("Booking details", await (await _client.GetAsync($"/gig-imports/{batchId}/source", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var check = _factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(context.GigImportBatches.Where(value => value.SourceIntakeId == id));
        Assert.Single(context.GigImportDrafts.Where(value => value.BatchId == batchId));
        Assert.Empty(context.Gigs.Where(value => value.SourceImportBatchId == batchId));
        Assert.Empty(context.GigExpenses);
        Assert.Empty(context.CurrentIntakes);
    }

    [Fact]
    public async Task IntentOverride_PersistsAcrossRetryAndDoesNotCreateBusinessData()
    {
        var id = Guid.NewGuid();
        (await _client.PostAsJsonAsync("/intake/current", new { intakeId = id, sourceType = "text", value = "Booking details" }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var changed = await _client.PutAsJsonAsync("/intake/current/intent", new { intakeId = id, intent = "Gigs" }, TestContext.Current.CancellationToken);
        changed.EnsureSuccessStatusCode();
        Assert.Equal("Gigs", (await changed.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken)).GetProperty("intentOverride").GetString());
        (await _client.PostAsJsonAsync("/intake/current/retry", new { intakeId = id }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var current = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>("/intake/current", TestContext.Current.CancellationToken);
        Assert.Equal("Gigs", current.GetProperty("intentOverride").GetString());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(db.GigExpenses);
        Assert.Empty(db.GigImportBatches.Where(value => value.SourceIntakeId == id));
    }

    [Fact]
    public async Task GigPreparation_UnavailableProviderLeavesSourceAndManualFallback()
    {
        var id = Guid.NewGuid();
        (await _client.PostAsJsonAsync("/intake/current", new { intakeId = id, sourceType = "text", value = "A gig to review" }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var queued = await _client.PostAsJsonAsync("/intake/current/gig-drafts", new { intakeId = id }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, queued.StatusCode);
        System.Text.Json.JsonElement status = default;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
            status = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/intake/current/gig-drafts/status?intakeId={id}", TestContext.Current.CancellationToken);
            if (status.GetProperty("state").GetString() == "Failed") break;
        }
        Assert.Equal("Failed", status.GetProperty("state").GetString());
        var current = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>("/intake/current", TestContext.Current.CancellationToken);
        Assert.Equal("A gig to review", current.GetProperty("sourceText").GetString());
        var manual = await _client.PostAsJsonAsync("/intake/current/gig-drafts/manual", new { intakeId = id }, TestContext.Current.CancellationToken);
        manual.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CurrentSource_RequiresCurrentIdentity()
    {
        var id = Guid.NewGuid();
        (await _client.PostAsJsonAsync("/intake/current", new { intakeId = id, sourceType = "text", value = "  Private booking information\n" }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var source = await _client.GetAsync($"/intake/current/source?intakeId={id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, source.StatusCode);
        Assert.Equal("  Private booking information\n", await source.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/intake/current/source?intakeId={Guid.NewGuid()}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task PdfAnalysis_RunsInBackgroundAndRetainsSourceOnFailure()
    {
        var id = Guid.NewGuid();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("synthetic pdf"u8.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "booking.pdf");
        form.Add(new StringContent(id.ToString()), "intakeId");
        var response = await _client.PostAsync("/intake/current/file", form, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.True((await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken)).GetProperty("analysisInBackground").GetBoolean());
        System.Text.Json.JsonElement current = default;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
            current = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>("/intake/current", TestContext.Current.CancellationToken);
            if (!current.GetProperty("analysisInBackground").GetBoolean()) break;
        }
        Assert.False(current.GetProperty("analysisInBackground").GetBoolean());
        Assert.Equal("Failed", current.GetProperty("analysisState").GetString());
        var source = await _client.GetAsync($"/intake/current/source?intakeId={id}", TestContext.Current.CancellationToken);
        Assert.Equal("synthetic pdf", await source.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GigPreparation_CreatesOneRecoverableBatchWithNoLiveGigs()
    {
        await using var isolated = new GlovellyApiFactory();
        await using var configured = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIntakeGigExtractor>();
            services.AddScoped<IIntakeGigExtractor, FakeGigExtractor>();
        }));
        using var client = configured.CreateClient();
        var id = Guid.NewGuid();
        int originalClientCount;
        await using (var setup = configured.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            originalClientCount = await db.Clients.CountAsync(TestContext.Current.CancellationToken);
            db.CurrentIntakes.Add(new CurrentIntake { Id = id, UserId = TestAuthContext.UserId, SourceType = IntakeSourceType.Text, Text = "First show\nSecond show", SourceFingerprint = "source", Intent = IntakeIntent.Gigs, AnalysisState = IntakeAnalysisState.Succeeded });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var requests = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.PostAsJsonAsync("/intake/current/gig-drafts", new { intakeId = id }, TestContext.Current.CancellationToken)));
        Assert.All(requests, value => Assert.True(value.IsSuccessStatusCode));
        await using (var signalScope = configured.Services.CreateAsyncScope())
        {
            var wake = await signalScope.ServiceProvider.GetRequiredService<IScheduledTaskStateStore>().ReadAsync<IntakePreparationTaskState>(ScheduledTaskNames.IntakePreparation, TestContext.Current.CancellationToken);
            Assert.True(wake?.State.HasPendingWork);
        }
        System.Text.Json.JsonElement status = default;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
            status = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/intake/current/gig-drafts/status?intakeId={id}", TestContext.Current.CancellationToken);
            if (status.GetProperty("state").GetString() == "Completed") break;
        }
        Assert.True(status.GetProperty("state").GetString() == "Completed", status.ToString());
        var batchId = status.GetProperty("batchId").GetGuid();
        var recovered = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/intake/gig-drafts/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(batchId, recovered.GetProperty("batchId").GetGuid());
        using var otherRequest = new HttpRequestMessage(HttpMethod.Get, $"/intake/gig-drafts/{id}");
        otherRequest.Headers.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(otherRequest, TestContext.Current.CancellationToken)).StatusCode);
        await using var verify = configured.Services.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(context.GigImportBatches.Where(value => value.SourceIntakeId == id));
        Assert.Equal(2, context.GigImportDrafts.Count(value => value.BatchId == batchId));
        Assert.Empty(context.Gigs.Where(value => value.SourceImportBatchId == batchId));
        Assert.Empty(context.GigExpenses);
        Assert.Empty(context.GigExternalResources);
        Assert.Empty(context.GigSetListImports);
        Assert.Empty(context.ExpenseAttachments);
        Assert.Empty(context.GigExternalResourceAttachments);
        Assert.Equal(originalClientCount, context.Clients.Count());
    }

    [Fact]
    public async Task PendingPreparation_SupersededOverrideCannotCreateBatch()
    {
        var id = Guid.NewGuid();
        var gate = _factory.Services.GetRequiredService<IntakeCommandLocks>().ForUser(TestAuthContext.UserId);
        await gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var intake = new CurrentIntake { Id = id, UserId = TestAuthContext.UserId, SourceType = IntakeSourceType.Text, Text = "Source", SourceFingerprint = "source", IntentOverride = IntakeIntent.Gigs, PreparationId = Guid.NewGuid(), PreparationState = IntakePreparationState.Pending };
            db.CurrentIntakes.Add(intake);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Task.Delay(3500, TestContext.Current.CancellationToken);
            intake.IntentOverride = IntakeIntent.Resource;
            intake.PreparationId = null;
            intake.PreparationState = IntakePreparationState.None;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        finally { gate.Release(); }
        await Task.Delay(500, TestContext.Current.CancellationToken);
        await using var verification = _factory.Services.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(context.GigImportBatches.Where(value => value.SourceIntakeId == id));
        Assert.Equal(IntakeIntent.Resource, (await context.CurrentIntakes.SingleAsync(value => value.Id == id, TestContext.Current.CancellationToken)).IntentOverride);
    }

    [Fact]
    public async Task GoogleDocManualDraft_RetainsSnapshotAndOriginalUrl()
    {
        var id = Guid.NewGuid();
        const string url = "https://docs.google.com/document/d/synthetic/edit";
        await using (var setup = _factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CurrentIntakes.Add(new CurrentIntake { Id = id, UserId = TestAuthContext.UserId, SourceType = IntakeSourceType.Url, Url = url, Text = "First show\nSecond show", SourceFingerprint = "doc-source", IntentOverride = IntakeIntent.Gigs, AnalysisState = IntakeAnalysisState.Failed });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var response = await _client.PostAsJsonAsync("/intake/current/gig-drafts/manual", new { intakeId = id }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var batchId = (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken)).GetProperty("batchId").GetGuid();
        var download = await _client.GetAsync($"/gig-imports/{batchId}/source", TestContext.Current.CancellationToken);
        Assert.Equal("First show\nSecond show", await download.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await using var verify = _factory.Services.CreateAsyncScope();
        var batch = await verify.ServiceProvider.GetRequiredService<AppDbContext>().GigImportBatches.SingleAsync(value => value.Id == batchId, TestContext.Current.CancellationToken);
        Assert.Equal(url, batch.SourceUrl);
    }

    [Fact]
    public async Task GoogleDocWithoutPermission_RetainsUrlAndCanBeAttachedAsResource()
    {
        var id = Guid.NewGuid();
        var gigId = Guid.NewGuid();
        const string url = "https://docs.google.com/document/d/example/edit";
        await using (var setup = _factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig { Id = gigId, ClientId = TestData.RiversideId, Title = "Existing gig", Date = new DateOnly(2027, 1, 1), Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        (await _client.PostAsJsonAsync("/intake/current", new { intakeId = id, sourceType = "url", value = url }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        System.Text.Json.JsonElement current = default;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
            current = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>("/intake/current", TestContext.Current.CancellationToken);
            if (!current.GetProperty("analysisInBackground").GetBoolean()) break;
        }
        Assert.Equal("document_connection_required", current.GetProperty("failureCode").GetString());
        Assert.Equal(url, current.GetProperty("sourceUrl").GetString());
        var response = await _client.PostAsJsonAsync("/intake/current/apply", new { intakeId = id, intent = "Resource", gigId, resource = new { resourceType = "GoogleDoc", purpose = "Other", title = "Booking document", notes = (string?)null, isPrimary = false } }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        await using var verify = _factory.Services.CreateAsyncScope();
        var verificationDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(verificationDb.GigExternalResources.Where(value => value.Id == id && value.GigId == gigId));
        Assert.Empty(verificationDb.GigSetListImports);
    }

    [Theory]
    [InlineData("GBP", ReceiptAnalysisConfidence.High, 24.5)]
    [InlineData("USD", ReceiptAnalysisConfidence.High, 0)]
    [InlineData(null, ReceiptAnalysisConfidence.None, 0)]
    public async Task ReceiptApplication_RequiresConfirmedGbpForAmountAndPromotesAnalysis(string? currency, ReceiptAnalysisConfidence confidence, double expectedAmount)
    {
        var id = Guid.NewGuid();
        var gigId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig { Id = gigId, ClientId = TestData.RiversideId, Title = "Currency receipt", Date = new DateOnly(2026, 1, 1), Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId });
            var evidence = ReceiptEvidence(currency, confidence);
            var analysis = ReceiptAnalysisEvidence.Read(evidence)!;
            var intake = new CurrentIntake { Id = id, UserId = TestAuthContext.UserId, SourceFingerprint = ReceiptFingerprint, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = "intakes/currency", AnalysisState = IntakeAnalysisState.Succeeded, Intent = IntakeIntent.Receipt, Confidence = ReceiptAnalysisConfidence.High, EvidenceJson = evidence };
            intake.AnalysisAttempts.Add(new IntakeAnalysisAttempt { Id = analysis.Id, CurrentIntakeId = id, State = IntakeAnalysisState.Succeeded, Intent = IntakeIntent.Receipt, Confidence = ReceiptAnalysisConfidence.High, Provider = analysis.Provider, Model = analysis.Model, PromptVersion = analysis.PromptVersion, EvidenceJson = evidence });
            db.CurrentIntakes.Add(intake);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().SaveAsync("intakes/currency", new MemoryStream("receipt"u8.ToArray()), "image/jpeg", TestContext.Current.CancellationToken);
        }
        var response = await ReplayReceiptAsync(_client, id);
        response.EnsureSuccessStatusCode();
        using var check = _factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal((decimal)expectedAmount, context.GigExpenses.Single().Amount);
        Assert.Equal(currency, context.ReceiptAnalyses.Single().Currency);
        Assert.Empty(context.CurrentIntakes);
        var recovered = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/intake/applications/{id}", TestContext.Current.CancellationToken);
        Assert.Equal("High", recovered.GetProperty("analysis").GetProperty("totalAmount").GetProperty("confidence").GetString());
    }

    [Fact]
    public async Task SourceReplacementAndStaleCommands_CannotUseAnotherIntakeIdentity()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        (await _client.PostAsJsonAsync("/intake/current", new { intakeId = firstId, sourceType = "text", value = "Source A" }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/intake/current", new { intakeId = firstId, sourceType = "text", value = "Source B" }, TestContext.Current.CancellationToken)).StatusCode);
        (await _client.PostAsJsonAsync("/intake/current", new { intakeId = secondId, sourceType = "text", value = "Source B" }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/intake/current/retry", new { intakeId = firstId }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/intake/current?intakeId={firstId}", TestContext.Current.CancellationToken)).StatusCode);
        var current = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>("/intake/current", TestContext.Current.CancellationToken);
        Assert.Equal("Source B", current.GetProperty("sourceText").GetString());
    }

    [Fact]
    public async Task MetadataOnlyAcquisition_IsNotLimitedByTheProviderBudget()
    {
        for (var index = 0; index < 12; index++)
            (await _client.PostAsJsonAsync("/intake/current", new { sourceType = "url", value = $"https://example.test/{index}" }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task FileSubmission_AnalysesThenSavesAndCanRecoverItsApplicationWithoutPrivateIntake()
    {
        await using var configured = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped(provider => new IntakeAnalysisService(new ReceiptExtractionEngine(
                (_, _, _, _) => Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = """{"merchant":"Rail Co","merchantConfidence":"high","totalAmount":"24.50","totalAmountConfidence":"high","warnings":[]}""" }] } }] }),
                provider.GetRequiredService<IExpenseAttachmentStore>(),
                Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" }),
                provider.GetRequiredService<TimeProvider>(), NullLogger<ReceiptExtractionEngine>.Instance,
                provider.GetRequiredService<ReceiptAnalysisBudget>()), provider.GetRequiredService<TimeProvider>()))));
        using var client = configured.CreateClient();
        var gigId = Guid.NewGuid();
        var intakeId = Guid.NewGuid();
        await using (var scope = configured.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig { Id = gigId, ClientId = TestData.RiversideId, Title = "Today's gig", Date = new DateOnly(2026, 1, 1), Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("receipt"u8.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "receipt.jpg");
        form.Add(new StringContent(intakeId.ToString()), "intakeId");

        var response = await client.PostAsync("/intake/current/file", form, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(gigId, payload.GetProperty("application").GetProperty("gig").GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/intake/current", TestContext.Current.CancellationToken)).StatusCode);
        var recovered = await client.GetAsync($"/intake/applications/{intakeId}", TestContext.Current.CancellationToken);
        recovered.EnsureSuccessStatusCode();
        using var check = configured.Services.CreateScope();
        Assert.Single(check.ServiceProvider.GetRequiredService<AppDbContext>().GigExpenses);
    }

    [Fact]
    public async Task PastedText_ExplicitResourceApplicationRetainsTheOriginalSource()
    {
        var gigId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig { Id = gigId, ClientId = TestData.RiversideId, Title = "Notes", Date = new DateOnly(2026, 1, 1), Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        const string text = "Booking notes: arrive at 6pm";
        var intakeId = Guid.NewGuid();
        var upload = await _client.PostAsJsonAsync("/intake/current", new { intakeId, sourceType = "text", value = text }, TestContext.Current.CancellationToken);
        upload.EnsureSuccessStatusCode();
        var body = new { intakeId, intent = "Resource", gigId, resource = new { resourceType = "File", purpose = "GigPlan", title = "Booking notes", isPrimary = false } };
        var response = await _client.PostAsJsonAsync("/intake/current/apply", body, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/intake/current/apply", body, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        using var verification = _factory.Services.CreateScope();
        var dbCheck = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var resource = Assert.Single(dbCheck.GigExternalResources.Include(value => value.Attachments));
        var attachment = Assert.Single(resource.Attachments);
        var source = await verification.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().OpenReadAsync(attachment.StorageKey, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(source.Content);
        Assert.Equal(text, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Empty(dbCheck.CurrentIntakes);
    }

    [Fact]
    public async Task SavedReceiptDeletion_RemovesOnlyItsExpenseAndBlobAndRefreshesTheDraftInvoice()
    {
        var gigId = Guid.NewGuid();
        var intakeId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig { Id = gigId, ClientId = TestData.RiversideId, InvoiceId = TestData.RiversideInvoiceId, Title = "Receipt deletion", Date = new DateOnly(2026, 1, 1), Venue = "Venue", Status = GigStatus.Completed, CreatedByUserId = TestAuthContext.UserId });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        (await _client.PostAsJsonAsync("/intake/current", new { intakeId, sourceType = "text", value = "Train receipt" }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/intake/current/apply", new { intakeId, intent = "Receipt", gigId }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        (await _client.PatchAsJsonAsync($"/gigs/receipt-drafts/{intakeId}", new { gigId, description = "Train fare", amount = 24.50m }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        string storageKey;
        using (var scope = _factory.Services.CreateScope()) storageKey = scope.ServiceProvider.GetRequiredService<AppDbContext>().ExpenseAttachments.Single().StorageKey;
        using var forbidden = new HttpRequestMessage(HttpMethod.Delete, $"/gigs/receipt-drafts/{intakeId}");
        forbidden.Headers.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(forbidden, TestContext.Current.CancellationToken)).StatusCode);

        var response = await _client.DeleteAsync($"/gigs/receipt-drafts/{intakeId}", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.Empty(payload.GetProperty("gig").GetProperty("expenses").EnumerateArray());
        using var verification = _factory.Services.CreateScope();
        var dbCheck = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(dbCheck.GigExpenses);
        var invoice = dbCheck.Invoices.Include(value => value.Lines).Single(value => value.Id == TestData.RiversideInvoiceId);
        Assert.DoesNotContain(invoice.Lines, value => value.Description == "Train fare");
        await Assert.ThrowsAsync<FileNotFoundException>(() => verification.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().OpenReadAsync(storageKey, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CurrentIntake_AndApplicationRecoveryAreOwnerScoped()
    {
        var response = await _client.PostAsJsonAsync("/intake/current", new { sourceType = "url", value = "https://example.test/private" }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        using var currentRequest = new HttpRequestMessage(HttpMethod.Get, "/intake/current");
        currentRequest.Headers.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(currentRequest, TestContext.Current.CancellationToken)).StatusCode);
        using var recoveryRequest = new HttpRequestMessage(HttpMethod.Get, $"/intake/applications/{payload.GetProperty("id").GetGuid()}");
        recoveryRequest.Headers.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(recoveryRequest, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task AutomaticApplication_StorageFailureKeepsTheSourceAndCreatesNoExpense()
    {
        var destinationStore = new FailingDestinationStore();
        await using var configured = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IExpenseAttachmentStore>();
            services.AddSingleton<IExpenseAttachmentStore>(destinationStore);
        }));
        using var client = configured.CreateClient();
        var intakeId = Guid.NewGuid();
        await using (var scope = configured.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig { Id = Guid.NewGuid(), ClientId = TestData.RiversideId, Title = "Today's gig", Date = new DateOnly(2026, 1, 1), Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId });
            db.CurrentIntakes.Add(new CurrentIntake { Id = intakeId, UserId = TestAuthContext.UserId, SourceFingerprint = ReceiptFingerprint, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = "intakes/test/failure", AnalysisState = IntakeAnalysisState.Succeeded, Intent = IntakeIntent.Receipt, Confidence = ReceiptAnalysisConfidence.High, EvidenceJson = ReceiptEvidence() });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().SaveAsync("intakes/test/failure", new MemoryStream("receipt"u8.ToArray()), "image/jpeg", TestContext.Current.CancellationToken);
        }
        var response = await ReplayReceiptAsync(client, intakeId);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, payload.GetProperty("application").ValueKind);
        Assert.Contains("could not be attached", payload.GetProperty("applicationError").GetString());
        using var verification = configured.Services.CreateScope();
        var dbCheck = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(dbCheck.CurrentIntakes);
        Assert.Empty(dbCheck.GigExpenses);
        Assert.Empty(dbCheck.ExpenseAttachments);
        var source = await verification.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().OpenReadAsync("intakes/test/failure", TestContext.Current.CancellationToken);
        await source.Content.DisposeAsync();

        destinationStore.FailDestinationWrites = false;
        var retry = await ReplayReceiptAsync(client, intakeId);
        retry.EnsureSuccessStatusCode();
        using var retryVerification = configured.Services.CreateScope();
        var recovered = retryVerification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(recovered.CurrentIntakes);
        Assert.Single(recovered.GigExpenses);
        Assert.Single(recovered.ExpenseAttachments);
    }

    private sealed class FailingDestinationStore : IExpenseAttachmentStore
    {
        private readonly InMemoryExpenseAttachmentStore _inner = new();
        public bool FailDestinationWrites { get; set; } = true;
        public Task SaveAsync(string key, Stream content, string contentType, CancellationToken token = default) => !FailDestinationWrites || key.StartsWith("intakes/", StringComparison.Ordinal)
            ? _inner.SaveAsync(key, content, contentType, token) : throw new IOException("Simulated destination write failure.");
        public Task<ExpenseAttachmentContent> OpenReadAsync(string key, CancellationToken token = default) => _inner.OpenReadAsync(key, token);
        public Task DeleteAsync(string key, CancellationToken token = default) => _inner.DeleteAsync(key, token);
    }

    [Fact]
    public async Task CleanupFailure_DoesNotSkipFinancialRefreshOrReportAttachmentAsUnsaved()
    {
        await using var configured = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IExpenseAttachmentStore>(); services.AddSingleton<IExpenseAttachmentStore, FailingCleanupStore>();
        }));
        using var client = configured.CreateClient();
        var id = Guid.NewGuid();
        using (var scope = configured.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig { Id = Guid.NewGuid(), InvoiceId = TestData.RiversideInvoiceId, ClientId = TestData.RiversideId, Title = "Financial refresh", Date = new DateOnly(2026, 1, 1), Venue = "Venue", Status = GigStatus.Completed, CreatedByUserId = TestAuthContext.UserId });
            db.CurrentIntakes.Add(new CurrentIntake { Id = id, UserId = TestAuthContext.UserId, SourceFingerprint = ReceiptFingerprint, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = "intakes/cleanup-failure", AnalysisState = IntakeAnalysisState.Succeeded, Intent = IntakeIntent.Receipt, Confidence = ReceiptAnalysisConfidence.High, EvidenceJson = ReceiptEvidence() });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().SaveAsync("intakes/cleanup-failure", new MemoryStream("receipt"u8.ToArray()), "image/jpeg", TestContext.Current.CancellationToken);
        }
        (await ReplayReceiptAsync(client, id)).EnsureSuccessStatusCode();
        using var check = configured.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = context.Invoices.Include(value => value.Lines).Single(value => value.Id == TestData.RiversideInvoiceId);
        Assert.Contains(invoice.Lines, line => line.Description == "Rail Co" && line.LineTotal == 24.50m);
        Assert.False(invoice.GigLinesNeedRefresh);
        Assert.Equal(InvoiceDocumentState.Current, invoice.DocumentState);
        Assert.Single(context.GigExpenses);
    }
    private sealed class FailingCleanupStore : IExpenseAttachmentStore
    {
        private readonly InMemoryExpenseAttachmentStore _inner = new();
        public Task SaveAsync(string key, Stream content, string contentType, CancellationToken token = default) => _inner.SaveAsync(key, content, contentType, token);
        public Task<ExpenseAttachmentContent> OpenReadAsync(string key, CancellationToken token = default) => _inner.OpenReadAsync(key, token);
        public Task DeleteAsync(string key, CancellationToken token = default) => key.StartsWith("intakes/", StringComparison.Ordinal) ? throw new IOException("Simulated cleanup failure.") : _inner.DeleteAsync(key, token);
    }

    [Fact]
    public async Task TextIntake_IsRetainedAndCreatesNoBusinessData()
    {
        var response = await _client.PostAsJsonAsync("/intake/current", new { sourceType = "text", value = "Set list notes" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var intake = await db.CurrentIntakes.Include(value => value.AnalysisAttempts).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(IntakeSourceType.Text, intake.SourceType);
        Assert.Equal(IntakeIntent.Unknown, intake.Intent);
        Assert.Single(intake.AnalysisAttempts);
        Assert.Empty(db.GigExpenses);
        Assert.Empty(db.GigExternalResources);
    }

    [Fact]
    public async Task Replacement_KeepsOnlyTheMostRecentCurrentIntake()
    {
        await _client.PostAsJsonAsync("/intake/current", new { sourceType = "text", value = "First" }, TestContext.Current.CancellationToken);
        var response = await _client.PostAsJsonAsync("/intake/current", new { sourceType = "url", value = "https://example.test/plan" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var intake = await db.CurrentIntakes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(IntakeSourceType.Url, intake.SourceType);
        Assert.Equal("https://example.test/plan", intake.Url);
    }

    [Fact]
    public async Task InvalidUrl_IsRejectedWithoutReplacingCurrentIntake()
    {
        await _client.PostAsJsonAsync("/intake/current", new { sourceType = "text", value = "Retain this" }, TestContext.Current.CancellationToken);
        var response = await _client.PostAsJsonAsync("/intake/current", new { sourceType = "url", value = "file:///etc/passwd" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Retain this", (await db.CurrentIntakes.SingleAsync(TestContext.Current.CancellationToken)).Text);
    }

    [Fact]
    public async Task ReceiptApplication_InitializesHighConfidenceValuesAndRefreshesDraftInvoiceWithoutChangingWordingOrReimbursement()
    {
        var gigId = Guid.NewGuid();
        var intakeId = Guid.NewGuid();
        var storageKey = "intakes/test/receipt";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig
            {
                Id = gigId,
                ClientId = TestData.RiversideId,
                InvoiceId = TestData.RiversideInvoiceId,
                Title = "Draft invoice receipt gig",
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Venue = "Venue",
                Status = GigStatus.Completed,
                CreatedByUserId = TestAuthContext.UserId,
                UpdatedByUserId = TestAuthContext.UserId,
            });
            db.CurrentIntakes.Add(new CurrentIntake
            {
                Id = intakeId,
                UserId = TestAuthContext.UserId,
                SourceType = IntakeSourceType.File,
                FileName = "receipt.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 7,
                StorageKey = storageKey,
                AnalysisState = IntakeAnalysisState.Succeeded,
                Intent = IntakeIntent.Receipt,
                Confidence = ReceiptAnalysisConfidence.Low,
                EvidenceJson = ReceiptEvidence(), SourceFingerprint = ReceiptFingerprint,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var setupStore = scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
            await using var source = new MemoryStream("receipt"u8.ToArray());
            await setupStore.SaveAsync(storageKey, source, "image/jpeg", TestContext.Current.CancellationToken);
        }

        var response = await _client.PostAsJsonAsync("/intake/current/apply", new { intakeId, intent = "Receipt", gigId }, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expense = await verificationDb.GigExpenses.SingleAsync(TestContext.Current.CancellationToken);
        var invoice = await verificationDb.Invoices.Include(value => value.Lines).SingleAsync(value => value.Id == TestData.RiversideInvoiceId, TestContext.Current.CancellationToken);

        Assert.Equal("Rail Co", expense.Description);
        Assert.Equal(24.50m, expense.Amount);
        Assert.Equal(GigExpenseCategory.Travel, expense.Category);
        Assert.Equal(GigExpenseReimbursementStatus.Unreimbursed, expense.ReimbursementStatus);
        Assert.Empty(verificationDb.CurrentIntakes);
        var invoiceExpense = Assert.Single(invoice.Lines, value => value.Type == InvoiceLineType.MiscExpense);
        Assert.Equal("Rail Co", invoiceExpense.Description);
        Assert.DoesNotContain("Travel", invoiceExpense.Description);

        var currentIntakeResponse = await _client.GetAsync("/intake/current", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, currentIntakeResponse.StatusCode);
        var store = verificationScope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.OpenReadAsync(storageKey, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReceiptApplication_RepeatedRequestsRecoverTheOriginalSave()
    {
        var gigId = Guid.NewGuid();
        var intakeId = Guid.NewGuid();
        const string storageKey = "intakes/test/nearby-receipt";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            db.Users.Single(value => value.Id == TestAuthContext.UserId).AutomaticReceiptMatching = AutomaticReceiptMatching.HighConfidence;
            db.Gigs.AddRange(
                new Gig { Id = gigId, ClientId = TestData.RiversideId, Title = "Nearby gig", Date = today, Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId, UpdatedByUserId = TestAuthContext.UserId },
                new Gig { Id = Guid.NewGuid(), ClientId = TestData.RiversideId, Title = "Distant gig", Date = today.AddDays(31), Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId, UpdatedByUserId = TestAuthContext.UserId });
            db.CurrentIntakes.Add(new CurrentIntake { Id = intakeId, UserId = TestAuthContext.UserId, SourceFingerprint = ReceiptFingerprint, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = storageKey, AnalysisState = IntakeAnalysisState.Succeeded, Intent = IntakeIntent.Receipt, Confidence = ReceiptAnalysisConfidence.High, EvidenceJson = ReceiptEvidence(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var store = scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
            await using var source = new MemoryStream("receipt"u8.ToArray());
            await store.SaveAsync(storageKey, source, "image/jpeg", TestContext.Current.CancellationToken);
        }

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => _client.PostAsJsonAsync("/intake/current/apply", new { intakeId, intent = "Receipt", gigId }, TestContext.Current.CancellationToken)));
        var response = responses[0];
        responses[1].EnsureSuccessStatusCode();
        var recovered = await _client.GetAsync($"/intake/applications/{intakeId}", TestContext.Current.CancellationToken);
        recovered.EnsureSuccessStatusCode();

        response.EnsureSuccessStatusCode();
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var expense = await verificationScope.ServiceProvider.GetRequiredService<AppDbContext>().GigExpenses.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Rail Co", expense.Description);
        Assert.Equal(24.50m, expense.Amount);
    }

    [Theory]
    [InlineData(AutomaticReceiptMatching.ManualOnly, ReceiptAnalysisConfidence.High, false)]
    [InlineData(AutomaticReceiptMatching.MediumConfidence, ReceiptAnalysisConfidence.High, true)]
    [InlineData(AutomaticReceiptMatching.HighConfidence, ReceiptAnalysisConfidence.High, true)]
    [InlineData(AutomaticReceiptMatching.VeryHighConfidence, ReceiptAnalysisConfidence.High, true)]
    [InlineData(AutomaticReceiptMatching.MediumConfidence, ReceiptAnalysisConfidence.Medium, true)]
    [InlineData(AutomaticReceiptMatching.HighConfidence, ReceiptAnalysisConfidence.Medium, false)]
    [InlineData(AutomaticReceiptMatching.MediumConfidence, ReceiptAnalysisConfidence.Low, false)]
    public async Task IntakeSubmission_ProactivelySavesAccordingToTheSavedPreference(AutomaticReceiptMatching preference, ReceiptAnalysisConfidence confidence, bool shouldAutoApply)
    {
        var gigId = Guid.NewGuid();
        var intakeId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.RemoveRange(db.Gigs);
            db.CurrentIntakes.RemoveRange(db.CurrentIntakes);
            db.Users.Single(value => value.Id == TestAuthContext.UserId).AutomaticReceiptMatching = preference;
            db.Gigs.Add(new Gig
            {
                Id = gigId,
                ClientId = TestData.RiversideId,
                Title = "Only nearby gig",
                Date = new DateOnly(2026, 1, 1),
                Venue = "Venue",
                Status = GigStatus.Confirmed,
                CreatedByUserId = TestAuthContext.UserId,
                UpdatedByUserId = TestAuthContext.UserId,
            });
            db.CurrentIntakes.Add(new CurrentIntake
            {
                Id = intakeId,
                UserId = TestAuthContext.UserId,
                SourceType = IntakeSourceType.File,
                FileName = "receipt.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 7,
                StorageKey = "intakes/test/preference",
                AnalysisState = IntakeAnalysisState.Succeeded,
                Intent = IntakeIntent.Receipt,
                Confidence = confidence,
                EvidenceJson = ReceiptEvidence(), SourceFingerprint = ReceiptFingerprint,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().SaveAsync("intakes/test/preference", new MemoryStream("receipt"u8.ToArray()), "image/jpeg", TestContext.Current.CancellationToken);
        }

        var response = await ReplayReceiptAsync(_client, intakeId);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(
            shouldAutoApply == (payload.GetProperty("application").ValueKind == System.Text.Json.JsonValueKind.Object),
            payload.GetRawText());
        if (shouldAutoApply)
        {
            Assert.Equal(gigId.ToString(), payload.GetProperty("application").GetProperty("gig").GetProperty("id").GetString());
        }
        else
        {
            using var scope = _factory.Services.CreateScope();
            Assert.Empty(scope.ServiceProvider.GetRequiredService<AppDbContext>().GigExpenses);
        }
    }

    [Fact]
    public async Task IntakeSubmission_ProactivelySavesToTheNearestGigWithMultipleCandidates()
    {
        var nearestGigId = Guid.NewGuid();
        var intakeId = Guid.NewGuid();
        var today = new DateOnly(2026, 1, 1);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.RemoveRange(db.Gigs);
            db.CurrentIntakes.RemoveRange(db.CurrentIntakes);
            db.Users.Single(value => value.Id == TestAuthContext.UserId).AutomaticReceiptMatching = AutomaticReceiptMatching.HighConfidence;
            db.Gigs.AddRange(
                new Gig { Id = nearestGigId, ClientId = TestData.RiversideId, Title = "Today", Date = today, Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId, UpdatedByUserId = TestAuthContext.UserId },
                new Gig { Id = Guid.NewGuid(), ClientId = TestData.RiversideId, Title = "Other", Date = today.AddDays(1), Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId, UpdatedByUserId = TestAuthContext.UserId });
            db.CurrentIntakes.Add(new CurrentIntake
            {
                Id = intakeId, UserId = TestAuthContext.UserId, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = "intakes/test/nearest", AnalysisState = IntakeAnalysisState.Succeeded, Intent = IntakeIntent.Receipt, Confidence = ReceiptAnalysisConfidence.High,
                EvidenceJson = ReceiptEvidence(), SourceFingerprint = ReceiptFingerprint, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().SaveAsync("intakes/test/nearest", new MemoryStream("receipt"u8.ToArray()), "image/jpeg", TestContext.Current.CancellationToken);
        }

        var response = await ReplayReceiptAsync(_client, intakeId);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(nearestGigId.ToString(), payload.GetProperty("application").GetProperty("gig").GetProperty("id").GetString());
        Assert.True(payload.GetProperty("application").GetProperty("hasNearbyCandidates").GetBoolean());
    }

    [Fact]
    public async Task DiscardCurrentIntake_RemovesOnlyTheCurrentIntakeAndItsTransientBlob()
    {
        const string storageKey = "intakes/test/discard";
        var intakeId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CurrentIntakes.Add(new CurrentIntake { Id = intakeId, UserId = TestAuthContext.UserId, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = storageKey, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var store = scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
            await using var source = new MemoryStream("receipt"u8.ToArray());
            await store.SaveAsync(storageKey, source, "image/jpeg", TestContext.Current.CancellationToken);
        }

        var response = await _client.DeleteAsync($"/intake/current?intakeId={intakeId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(verificationDb.CurrentIntakes);
        var verificationStore = verificationScope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
        await Assert.ThrowsAsync<FileNotFoundException>(() => verificationStore.OpenReadAsync(storageKey, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://docs.google.com/document/d/example/edit", "GoogleDoc")]
    [InlineData("https://docs.google.com/spreadsheets/d/example/edit", "GoogleSheet")]
    public async Task UrlIntake_SuggestsGoogleResourceTypeWithDisplaySafeEvidence(string url, string expectedResourceType)
    {
        var response = await _client.PostAsJsonAsync("/intake/current", new { sourceType = "url", value = url }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expectedResourceType, result.GetProperty("suggestedResourceType").GetString());
        var evidence = result.GetProperty("evidence").EnumerateArray().ToList();
        Assert.Contains(evidence, item => item.GetProperty("label").GetString() == "Link" && item.GetProperty("value").GetString() == "docs.google.com");
        Assert.Contains(evidence, item => item.GetProperty("label").GetString() == "Suggested type" && item.GetProperty("value").GetString() == expectedResourceType);
    }

    [Fact]
    public async Task ResourceApplication_ClearsCurrentIntakeAndDeletesTransientBlob()
    {
        var gigId = Guid.NewGuid();
        var intakeId = Guid.NewGuid();
        const string storageKey = "intakes/test/resource";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var setupDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            setupDb.Gigs.Add(new Gig
            {
                Id = gigId, ClientId = TestData.RiversideId, Title = "Resource gig", Date = DateOnly.FromDateTime(DateTime.UtcNow), Venue = "Venue", Status = GigStatus.Confirmed,
                CreatedByUserId = TestAuthContext.UserId, UpdatedByUserId = TestAuthContext.UserId,
            });
            setupDb.CurrentIntakes.Add(new CurrentIntake
            {
                Id = intakeId, UserId = TestAuthContext.UserId, SourceType = IntakeSourceType.File, FileName = "plan.pdf", ContentType = "application/pdf", SizeBytes = 8,
                StorageKey = storageKey, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
            await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            var setupStore = scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
            await using var source = new MemoryStream("resource"u8.ToArray());
            await setupStore.SaveAsync(storageKey, source, "application/pdf", TestContext.Current.CancellationToken);
        }

        var response = await _client.PostAsJsonAsync("/intake/current/apply", new
        {
            intakeId, intent = "Resource", gigId,
            resource = new { resourceType = "File", purpose = "GigPlan", title = "Stage plan", notes = (string?)null, isPrimary = true },
        }, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resource = await db.GigExternalResources.Include(value => value.Attachments).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Single(resource.Attachments);
        Assert.Empty(db.CurrentIntakes);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.GetAsync("/intake/current", TestContext.Current.CancellationToken)).StatusCode);
        var store = verificationScope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.OpenReadAsync(storageKey, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/intake/current/receipt")]
    [InlineData("/intake/current/resource")]
    [InlineData("/gigs/receipt-drafts")]
    [InlineData("/gigs/external-resource-drafts/file")]
    [InlineData("/gigs/external-resource-drafts/link")]
    public async Task SupersededApplicationRoutes_AreNotMapped(string route)
    {
        var response = await _client.PostAsJsonAsync(route, new { }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
