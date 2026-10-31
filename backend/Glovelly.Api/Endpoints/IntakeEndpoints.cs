using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class IntakeEndpoints
{
    public static RouteGroupBuilder MapIntakeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/current", async (HttpContext context, ICurrentUserAccessor currentUser, IntakeWorkflowService workflow) =>
        {
            var userId = currentUser.TryGetUserId(context.User);
            if (!userId.HasValue) return Results.Unauthorized();
            var intake = await workflow.FindAsync(userId.Value, context.RequestAborted);
            return intake is null ? Results.NoContent() : Results.Ok(await workflow.ReadAsync(intake, context.RequestAborted));
        });

        group.MapGet("/current/source", async (Guid intakeId, HttpContext context, ICurrentUserAccessor currentUser, IntakeWorkflowService workflow, IExpenseAttachmentStore store) =>
        {
            var userId = currentUser.TryGetUserId(context.User);
            if (!userId.HasValue) return Results.Unauthorized();
            var intake = await workflow.FindAsync(userId.Value, context.RequestAborted);
            if (intake is null || intake.Id != intakeId) return Results.NotFound();
            if (intake.SourceType is IntakeSourceType.Text or IntakeSourceType.Url && intake.Text is not null)
                return Results.File(System.Text.Encoding.UTF8.GetBytes(intake.Text), "text/plain", intake.SourceType == IntakeSourceType.Url ? "Google Doc snapshot.txt" : "Pasted text.txt");
            if (intake.StorageKey is null) return Results.NotFound();
            try
            {
                var source = await store.OpenReadAsync(intake.StorageKey, context.RequestAborted);
                return Results.File(source.Content, intake.ContentType ?? "application/octet-stream", intake.FileName ?? "source");
            }
            catch (FileNotFoundException) { return Results.NotFound(); }
        });

        group.MapGet("/applications/{intakeId:guid}", async (Guid intakeId, HttpContext context, ICurrentUserAccessor currentUser, IntakeApplicationService application) =>
        {
            var userId = currentUser.TryGetUserId(context.User);
            if (!userId.HasValue) return Results.Unauthorized();
            var saved = await application.FindAppliedAsync(intakeId, userId.Value, context.RequestAborted);
            return saved is null ? Results.NotFound() : Results.Ok(saved);
        });

        group.MapGet("/gig-drafts/{intakeId:guid}", async (Guid intakeId, HttpContext context, ICurrentUserAccessor currentUser, IntakeGigDraftService drafts) =>
        {
            var userId = currentUser.TryGetUserId(context.User);
            if (!userId.HasValue) return Results.Unauthorized();
            var saved = await drafts.FindAsync(intakeId, userId.Value, context.RequestAborted);
            return saved is null ? Results.NotFound() : Results.Ok(saved);
        });

        group.MapGet("/current/gig-drafts/status", async (Guid intakeId, HttpContext context, ICurrentUserAccessor currentUser, IntakeGigDraftService drafts, IntakeWorkflowService workflow) =>
        {
            var userId = currentUser.TryGetUserId(context.User);
            if (!userId.HasValue) return Results.Unauthorized();
            var saved = await drafts.FindAsync(intakeId, userId.Value, context.RequestAborted);
            if (saved is not null) return Results.Ok(new { state = "Completed", saved.BatchId });
            var intake = await workflow.FindAsync(userId.Value, context.RequestAborted);
            if (intake is null || intake.Id != intakeId) return Results.NotFound();
            return Results.Ok(new { state = intake.PreparationState.ToString(), error = intake.PreparationError });
        });

        group.MapPost("/current/gig-drafts/manual", (IntakeIdentityRequest request, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeGigDraftService drafts, IntakeApplicationService application) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                if (await application.FindAppliedAsync(request.IntakeId, userId, token) is not null)
                    return Results.Conflict(new { message = "This source has already been attached." });
                try
                {
                    var result = await drafts.PrepareAsync(request.IntakeId, userId, true, token);
                    return result is null ? EndpointSupport.ValidationProblem("source", "This source is unavailable for manual gig preparation. Use pasted text or a file.") : Results.Ok(result);
                }
                catch (InvalidOperationException error) { return EndpointSupport.ValidationProblem("intent", error.Message); }
            }));

        group.MapPost("/current/gig-drafts", (IntakeIdentityRequest request, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeGigDraftService drafts, IntakeApplicationService application, IntakeWorkflowService workflow, AppDbContext db) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                if (await application.FindAppliedAsync(request.IntakeId, userId, token) is not null) return Results.Conflict(new { message = "This source has already been attached." });
                var saved = await drafts.FindAsync(request.IntakeId, userId, token);
                if (saved is not null) return Results.Ok(new { state = "Completed", saved.BatchId });
                var intake = await workflow.FindAsync(userId, token);
                if (intake is null || intake.Id != request.IntakeId) return Results.NotFound();
                if (intake.IntentOverride is not null && intake.IntentOverride != IntakeIntent.Gigs) return EndpointSupport.ValidationProblem("intent", "Choose gig information before preparing drafts.");
                if (intake.SourceType == IntakeSourceType.Url && intake.Text is null) return EndpointSupport.ValidationProblem("source", "Read the Google Doc before preparing gigs.");
                if (intake.PreparationState is not (IntakePreparationState.Pending or IntakePreparationState.Running))
                {
                    intake.IntentOverride = IntakeIntent.Gigs;
                    intake.PreparationId = Guid.NewGuid();
                    intake.PreparationState = IntakePreparationState.Pending;
                    intake.PreparationError = null;
                    intake.PreparationStartedAt = null;
                    await db.SaveChangesAsync(token);
                    await SignalBackgroundAsync(context, token);
                }
                return Results.Accepted(value: new { state = intake.PreparationState.ToString() });
            }));

        group.MapPost("/current/file", (HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow, IntakeApplicationService application, IntakeGigDraftService drafts, IExpenseAttachmentStore store, IOptions<ExpenseAttachmentSettings> options, TimeProvider clock) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                if (!context.Request.HasFormContentType) return EndpointSupport.ValidationProblem("file", "Upload an intake file.");
                var form = await context.Request.ReadFormAsync(token);
                var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                var validation = GigEndpointSupport.ValidateExternalResourceAttachmentFile(file, options.Value);
                if (validation is not null) return validation;
                var id = Guid.TryParse(form["intakeId"], out var requestedId) && requestedId != Guid.Empty ? requestedId : Guid.NewGuid();
                var name = Path.GetFileName(file!.FileName);
                name = string.IsNullOrWhiteSpace(name) ? "upload" : name;
                using var buffer = new MemoryStream();
                await using (var content = file.OpenReadStream()) await content.CopyToAsync(buffer, token);
                var bytes = buffer.ToArray();
                var fingerprint = IntakeSourceIdentity.File(name, file.ContentType, bytes);
                var recovered = await RecoverAsync(id, userId, fingerprint, workflow, application, drafts, token);
                if (recovered is not null) return recovered;
                var intake = new CurrentIntake
                {
                    Id = id, UserId = userId, SourceType = IntakeSourceType.File,
                    FileName = name, SourceFingerprint = fingerprint,
                    ContentType = file.ContentType, SizeBytes = file.Length, AnalysisInBackground = string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase),
                    StorageKey = $"intakes/{userId}/{Guid.NewGuid():N}", CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(),
                };
                buffer.Position = 0;
                await store.SaveAsync(intake.StorageKey, buffer, intake.ContentType, token);
                await workflow.ReplaceAsync(intake, token);
                if (intake.AnalysisInBackground) await SignalBackgroundAsync(context, token);
                return Results.Created("/intake/current", intake.AnalysisInBackground ? await workflow.ReadAsync(intake, token) : await workflow.AnalyseAndApplyAsync(intake, token));
            }));

        group.MapPost("/current", (IntakeSourceRequest request, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow, IntakeApplicationService application, IntakeGigDraftService drafts, TimeProvider clock) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                var id = request.IntakeId is { } requestedId && requestedId != Guid.Empty ? requestedId : Guid.NewGuid();
                var intake = new CurrentIntake { Id = id, UserId = userId, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
                if (request.SourceType?.Equals("url", StringComparison.OrdinalIgnoreCase) == true && Uri.TryCreate(request.Value, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" && url.AbsoluteUri.Length <= IntakeSourceIdentity.MaxUrlLength)
                {
                    intake.SourceType = IntakeSourceType.Url;
                    intake.Url = url.AbsoluteUri;
                    intake.AnalysisInBackground = GoogleDocIntakeReader.TryGetDocumentId(intake.Url, out _);
                }
                else if (request.SourceType?.Equals("text", StringComparison.OrdinalIgnoreCase) == true && !string.IsNullOrWhiteSpace(request.Value) && request.Value.Length <= 16_000)
                {
                    intake.SourceType = IntakeSourceType.Text;
                    intake.Text = request.Value;
                }
                else return EndpointSupport.ValidationProblem("source", "Provide an absolute http or https URL, or text of 16,000 characters or fewer.");
                intake.SourceFingerprint = IntakeSourceIdentity.Text(intake.SourceType == IntakeSourceType.Url ? "url" : "text", intake.Url ?? intake.Text!);
                var recovered = await RecoverAsync(id, userId, intake.SourceFingerprint, workflow, application, drafts, token);
                if (recovered is not null) return recovered;
                await workflow.ReplaceAsync(intake, token);
                if (intake.AnalysisInBackground) await SignalBackgroundAsync(context, token);
                return Results.Created("/intake/current", intake.AnalysisInBackground ? await workflow.ReadAsync(intake, token) : await workflow.AnalyseAndApplyAsync(intake, token));
            }));

        group.MapPost("/current/retry", (IntakeIdentityRequest request, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                var intake = await workflow.FindAsync(userId, token);
                if (intake is null) return Results.NotFound();
                if (intake.Id != request.IntakeId) return Results.Conflict(new { message = "This upload was replaced. Reopen Add to Glovelly before retrying." });
                intake.PreparationState = IntakePreparationState.None;
                intake.PreparationId = null;
                if (intake.SourceType == IntakeSourceType.File && string.Equals(intake.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase) || intake.SourceType == IntakeSourceType.Url && GoogleDocIntakeReader.TryGetDocumentId(intake.Url, out _))
                {
                    intake.AnalysisInBackground = true;
                    intake.AnalysisState = IntakeAnalysisState.Pending;
                    await context.RequestServices.GetRequiredService<AppDbContext>().SaveChangesAsync(token);
                    await SignalBackgroundAsync(context, token);
                    return Results.Ok(await workflow.ReadAsync(intake, token));
                }
                return Results.Ok(await workflow.AnalyseAndApplyAsync(intake, token));
            }));

        group.MapPut("/current/intent", (IntakeIntentRequest request, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow, AppDbContext db) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                if (request.Intent is not (IntakeIntent.Receipt or IntakeIntent.Resource or IntakeIntent.Gigs))
                    return EndpointSupport.ValidationProblem("intent", "Choose receipt, resource or gigs.");
                var intake = await workflow.FindAsync(userId, token);
                if (intake is null) return Results.NotFound();
                if (intake.Id != request.IntakeId) return Results.Conflict(new { message = "This upload was replaced. Reopen Add to Glovelly." });
                if (request.Intent == IntakeIntent.Receipt && intake.SourceType == IntakeSourceType.Url)
                    return EndpointSupport.ValidationProblem("intent", "A URL cannot be used as a receipt.");
                intake.IntentOverride = request.Intent;
                intake.PreparationState = IntakePreparationState.None;
                intake.PreparationId = null;
                await db.SaveChangesAsync(token);
                return Results.Ok(await workflow.ReadAsync(intake, token));
            }));

        group.MapDelete("/current", (Guid intakeId, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                var current = await workflow.FindAsync(userId, token);
                if (current is not null && current.Id != intakeId) return Results.Conflict(new { message = "This upload was replaced. Reopen Add to Glovelly before discarding." });
                await workflow.DiscardAsync(userId, token);
                return Results.NoContent();
            }));

        group.MapPost("/current/apply", (IntakeApplyRequest request, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeApplicationService application) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                var result = await application.ApplyAsync(request.IntakeId, userId, request.Intent, request.GigId, request.Resource, token);
                return result.Application is not null ? Results.Ok(result.Application) : EndpointSupport.ValidationProblem(result.Field!, result.Error!);
            }));
        return group;
    }

    private static async Task<IResult?> RecoverAsync(Guid id, Guid userId, string fingerprint, IntakeWorkflowService workflow, IntakeApplicationService application, IntakeGigDraftService drafts, CancellationToken token)
    {
        if (await drafts.FindAsync(id, userId, token) is not null)
            return Results.Conflict(new { message = "This upload has already prepared a gig import. Recover the existing import or start a new upload." });
        var saved = await application.FindAppliedAsync(id, userId, token);
        if (saved is not null)
        {
            if (saved.SourceFingerprint != fingerprint) return Results.Conflict(new { message = "This upload ID belongs to a different source. Start a new upload for the replacement." });
            var recovered = await application.ApplyAsync(id, userId, saved.Kind, saved.Gig.Id, null, token);
            return Results.Ok(new { id, application = recovered.Application });
        }
        var current = await workflow.FindAsync(userId, token);
        if (current?.Id != id) return null;
        if (current.SourceFingerprint != fingerprint) return Results.Conflict(new { message = "This upload ID belongs to a different source. Start a new upload for the replacement." });
        return Results.Ok(current.AnalysisInBackground ? await workflow.ReadAsync(current, token) : current.AnalysisState == IntakeAnalysisState.Pending ? await workflow.AnalyseAndApplyAsync(current, token) : await workflow.CompleteAsync(current, token));
    }

    private static async Task<IResult> CommandAsync(HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, Func<Guid, CancellationToken, Task<IResult>> command)
    {
        var userId = currentUser.TryGetUserId(context.User);
        if (!userId.HasValue) return Results.Unauthorized();
        var gate = locks.ForUser(userId.Value);
        await gate.WaitAsync(context.RequestAborted);
        try
        {
            await using var scope = new IntakeCommandScope(context.RequestServices.GetRequiredService<AppDbContext>());
            await scope.AcquireAsync(userId.Value, context.RequestAborted);
            return await command(userId.Value, context.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(detail: "This upload changed while it was being saved. Reopen Add to Glovelly to recover it.", statusCode: StatusCodes.Status409Conflict);
        }
        finally { gate.Release(); }
    }

    private static async Task SignalBackgroundAsync(HttpContext context, CancellationToken token)
    {
        try { await context.RequestServices.GetRequiredService<IScheduledTaskSignal>().MarkStaleAsync(ScheduledTaskNames.IntakePreparation, "intake queued", token); }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("IntakeEndpoints").LogWarning("Unable to signal background intake work; periodic recovery will retry.");
        }
    }

    private sealed record IntakeSourceRequest(string? SourceType, string? Value, Guid? IntakeId);
    private sealed record IntakeIdentityRequest(Guid IntakeId);
    private sealed record IntakeIntentRequest(Guid IntakeId, IntakeIntent Intent);
    private sealed record IntakeApplyRequest(Guid IntakeId, IntakeIntent Intent, Guid GigId, IntakeResourceInput? Resource);
}
