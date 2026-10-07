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

        group.MapGet("/applications/{intakeId:guid}", async (Guid intakeId, HttpContext context, ICurrentUserAccessor currentUser, IntakeApplicationService application) =>
        {
            var userId = currentUser.TryGetUserId(context.User);
            if (!userId.HasValue) return Results.Unauthorized();
            var saved = await application.FindAppliedAsync(intakeId, userId.Value, context.RequestAborted);
            return saved is null ? Results.NotFound() : Results.Ok(saved);
        });

        group.MapGet("/current/candidates", async (string? continuation, AppDbContext db, HttpContext context, ICurrentUserAccessor currentUser, IOptions<QuickCaptureSettings> options, TimeProvider clock) =>
        {
            try
            {
                var result = await GigQuickCaptureSupport.QueryCandidatesAsync(db, currentUser.TryGetUserId(context.User), DateOnly.FromDateTime(clock.GetLocalNow().DateTime), GigQuickCaptureSupport.NormalizeSettings(options.Value), continuation, context.RequestAborted);
                return Results.Ok(new { candidates = GigQuickCaptureSupport.ToCandidateResponses(result.Candidates, null), hasMore = result.HasMore, continuation = result.Continuation });
            }
            catch (InvalidOperationException)
            {
                return EndpointSupport.ValidationProblem("continuation", "The candidate continuation is invalid.");
            }
        });

        group.MapPost("/current/file", (HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow, IntakeApplicationService application, IExpenseAttachmentStore store, IOptions<ExpenseAttachmentSettings> options, TimeProvider clock) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                if (!context.Request.HasFormContentType) return EndpointSupport.ValidationProblem("file", "Upload an intake file.");
                var form = await context.Request.ReadFormAsync(token);
                var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                var validation = GigEndpointSupport.ValidateExternalResourceAttachmentFile(file, options.Value);
                if (validation is not null) return validation;
                var id = Guid.TryParse(form["intakeId"], out var requestedId) && requestedId != Guid.Empty ? requestedId : Guid.NewGuid();
                var recovered = await RecoverAsync(id, userId, workflow, application, token);
                if (recovered is not null) return recovered;
                var name = Path.GetFileName(file!.FileName);
                var intake = new CurrentIntake
                {
                    Id = id, UserId = userId, SourceType = IntakeSourceType.File,
                    FileName = string.IsNullOrWhiteSpace(name) ? "upload" : name,
                    ContentType = file.ContentType, SizeBytes = file.Length,
                    StorageKey = $"intakes/{userId}/{id:N}", CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(),
                };
                await using (var content = file.OpenReadStream()) await store.SaveAsync(intake.StorageKey, content, intake.ContentType, token);
                await workflow.ReplaceAsync(intake, token);
                return Results.Created("/intake/current", await workflow.AnalyseAndApplyAsync(intake, token));
            })).RequireRateLimiting("ReceiptAnalysis");

        group.MapPost("/current", (IntakeSourceRequest request, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow, IntakeApplicationService application, TimeProvider clock) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                var id = request.IntakeId is { } requestedId && requestedId != Guid.Empty ? requestedId : Guid.NewGuid();
                var recovered = await RecoverAsync(id, userId, workflow, application, token);
                if (recovered is not null) return recovered;
                var intake = new CurrentIntake { Id = id, UserId = userId, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
                if (request.SourceType?.Equals("url", StringComparison.OrdinalIgnoreCase) == true && Uri.TryCreate(request.Value, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" && url.AbsoluteUri.Length <= 2048)
                {
                    intake.SourceType = IntakeSourceType.Url;
                    intake.Url = url.AbsoluteUri;
                }
                else if (request.SourceType?.Equals("text", StringComparison.OrdinalIgnoreCase) == true && !string.IsNullOrWhiteSpace(request.Value) && request.Value.Length <= 16_000)
                {
                    intake.SourceType = IntakeSourceType.Text;
                    intake.Text = request.Value.Trim();
                }
                else return EndpointSupport.ValidationProblem("source", "Provide an absolute http or https URL, or text of 16,000 characters or fewer.");
                await workflow.ReplaceAsync(intake, token);
                return Results.Created("/intake/current", await workflow.AnalyseAndApplyAsync(intake, token));
            })).RequireRateLimiting("ReceiptAnalysis");

        group.MapPost("/current/retry", (HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                var intake = await workflow.FindAsync(userId, token);
                return intake is null ? Results.NotFound() : Results.Ok(await workflow.AnalyseAndApplyAsync(intake, token));
            })).RequireRateLimiting("ReceiptAnalysis");

        group.MapDelete("/current", (HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
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

    private static async Task<IResult?> RecoverAsync(Guid id, Guid userId, IntakeWorkflowService workflow, IntakeApplicationService application, CancellationToken token)
    {
        var saved = await application.FindAppliedAsync(id, userId, token);
        if (saved is not null) return Results.Ok(new { id, application = saved });
        var current = await workflow.FindAsync(userId, token);
        return current?.Id == id ? Results.Ok(await workflow.CompleteAsync(current, token)) : null;
    }

    private static async Task<IResult> CommandAsync(HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, Func<Guid, CancellationToken, Task<IResult>> command)
    {
        var userId = currentUser.TryGetUserId(context.User);
        if (!userId.HasValue) return Results.Unauthorized();
        var gate = locks.ForUser(userId.Value);
        await gate.WaitAsync(context.RequestAborted);
        try { return await command(userId.Value, context.RequestAborted); }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(detail: "This upload changed while it was being saved. Reopen Add to Glovelly to recover it.", statusCode: StatusCodes.Status409Conflict);
        }
        finally { gate.Release(); }
    }

    private sealed record IntakeSourceRequest(string? SourceType, string? Value, Guid? IntakeId);
    private sealed record IntakeApplyRequest(Guid IntakeId, IntakeIntent Intent, Guid GigId, IntakeResourceInput? Resource);
}
