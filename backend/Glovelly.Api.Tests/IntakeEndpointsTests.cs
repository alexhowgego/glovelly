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
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public IntakeEndpointsTests()
    {
        _factory = new GlovellyApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose() { _client.Dispose(); _factory.Dispose(); }

    [Fact]
    public async Task FileSubmission_AnalysesThenSavesAndCanRecoverItsApplicationWithoutPrivateIntake()
    {
        await using var configured = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped(provider => new IntakeAnalysisService(
                (_, _, _, _) => Task.FromResult(new GenerateContentResponse { Candidates = [new Candidate { Content = new Content { Parts = [new Part { Text = """{"merchant":"Rail Co","merchantConfidence":"high","totalAmount":"24.50","totalAmountConfidence":"high","warnings":[]}""" }] } }] }),
                provider.GetRequiredService<IExpenseAttachmentStore>(),
                Options.Create(new ReceiptAnalysisSettings { Enabled = true, VertexAiProjectId = "test", VertexAiLocation = "eu", VertexAiModel = "test" }),
                provider.GetRequiredService<TimeProvider>(), NullLogger<IntakeAnalysisService>.Instance))));
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
        await using var configured = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IExpenseAttachmentStore>();
            services.AddSingleton<IExpenseAttachmentStore, FailingDestinationStore>();
        }));
        using var client = configured.CreateClient();
        var intakeId = Guid.NewGuid();
        await using (var scope = configured.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig { Id = Guid.NewGuid(), ClientId = TestData.RiversideId, Title = "Today's gig", Date = new DateOnly(2026, 1, 1), Venue = "Venue", Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId });
            db.CurrentIntakes.Add(new CurrentIntake { Id = intakeId, UserId = TestAuthContext.UserId, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = "intakes/test/failure", AnalysisState = IntakeAnalysisState.Succeeded, Intent = IntakeIntent.Receipt, Confidence = ReceiptAnalysisConfidence.High, EvidenceJson = "[]" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().SaveAsync("intakes/test/failure", new MemoryStream("receipt"u8.ToArray()), "image/jpeg", TestContext.Current.CancellationToken);
        }
        var response = await client.PostAsJsonAsync("/intake/current", new { intakeId, sourceType = "text", value = "Retry original source" }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, payload.GetProperty("application").ValueKind);
        Assert.Contains("could not be attached", payload.GetProperty("applicationError").GetString());
        using var verification = configured.Services.CreateScope();
        var dbCheck = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(dbCheck.CurrentIntakes);
        Assert.Empty(dbCheck.GigExpenses);
        var source = await verification.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().OpenReadAsync("intakes/test/failure", TestContext.Current.CancellationToken);
        await source.Content.DisposeAsync();
    }

    private sealed class FailingDestinationStore : IExpenseAttachmentStore
    {
        private readonly InMemoryExpenseAttachmentStore _inner = new();
        public Task SaveAsync(string key, Stream content, string contentType, CancellationToken token = default) => key.StartsWith("intakes/", StringComparison.Ordinal)
            ? _inner.SaveAsync(key, content, contentType, token) : throw new IOException("Simulated destination write failure.");
        public Task<ExpenseAttachmentContent> OpenReadAsync(string key, CancellationToken token = default) => _inner.OpenReadAsync(key, token);
        public Task DeleteAsync(string key, CancellationToken token = default) => _inner.DeleteAsync(key, token);
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
                EvidenceJson = "[{\"field\":\"merchant\",\"value\":\"Rail Co\",\"confidence\":\"High\"},{\"field\":\"totalAmount\",\"value\":\"24.50\",\"confidence\":\"High\"},{\"field\":\"suggestedCategory\",\"value\":\"Travel\",\"confidence\":\"High\"}]",
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
            db.CurrentIntakes.Add(new CurrentIntake { Id = intakeId, UserId = TestAuthContext.UserId, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = storageKey, AnalysisState = IntakeAnalysisState.Succeeded, Intent = IntakeIntent.Receipt, Confidence = ReceiptAnalysisConfidence.High, EvidenceJson = "[{\"field\":\"merchant\",\"value\":\"Rail Co\",\"confidence\":\"High\"},{\"field\":\"totalAmount\",\"value\":\"24.50\",\"confidence\":\"High\"}]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
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
                EvidenceJson = "[{\"field\":\"merchant\",\"value\":\"Rail Co\",\"confidence\":\"High\"},{\"field\":\"totalAmount\",\"value\":\"24.50\",\"confidence\":\"High\"}]",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().SaveAsync("intakes/test/preference", new MemoryStream("receipt"u8.ToArray()), "image/jpeg", TestContext.Current.CancellationToken);
        }

        var response = await _client.PostAsJsonAsync("/intake/current", new { intakeId, sourceType = "text", value = "Retry the original submission" }, TestContext.Current.CancellationToken);

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
                EvidenceJson = "[{\"field\":\"merchant\",\"value\":\"Rail Co\",\"confidence\":\"High\"},{\"field\":\"totalAmount\",\"value\":\"24.50\",\"confidence\":\"High\"}]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>().SaveAsync("intakes/test/nearest", new MemoryStream("receipt"u8.ToArray()), "image/jpeg", TestContext.Current.CancellationToken);
        }

        var response = await _client.PostAsJsonAsync("/intake/current", new { intakeId, sourceType = "text", value = "Retry original submission" }, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(nearestGigId.ToString(), payload.GetProperty("application").GetProperty("gig").GetProperty("id").GetString());
    }

    [Fact]
    public async Task DiscardCurrentIntake_RemovesOnlyTheCurrentIntakeAndItsTransientBlob()
    {
        const string storageKey = "intakes/test/discard";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CurrentIntakes.Add(new CurrentIntake { Id = Guid.NewGuid(), UserId = TestAuthContext.UserId, SourceType = IntakeSourceType.File, FileName = "receipt.jpg", ContentType = "image/jpeg", SizeBytes = 7, StorageKey = storageKey, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var store = scope.ServiceProvider.GetRequiredService<IExpenseAttachmentStore>();
            await using var source = new MemoryStream("receipt"u8.ToArray());
            await store.SaveAsync(storageKey, source, "image/jpeg", TestContext.Current.CancellationToken);
        }

        var response = await _client.DeleteAsync("/intake/current", TestContext.Current.CancellationToken);

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
