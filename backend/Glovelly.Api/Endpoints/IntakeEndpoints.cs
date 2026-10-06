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

        group.MapPost("/current/file", (HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow, IntakeApplicationService application, IExpenseAttachmentStore store, IOptions<ExpenseAttachmentSettings> options, TimeProvider clock) =>
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
                var recovered = await RecoverAsync(id, userId, fingerprint, workflow, application, token);
                if (recovered is not null) return recovered;
                var intake = new CurrentIntake
                {
                    Id = id, UserId = userId, SourceType = IntakeSourceType.File,
                    FileName = name, SourceFingerprint = fingerprint,
                    ContentType = file.ContentType, SizeBytes = file.Length,
                    StorageKey = $"intakes/{userId}/{Guid.NewGuid():N}", CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(),
                };
                buffer.Position = 0;
                await store.SaveAsync(intake.StorageKey, buffer, intake.ContentType, token);
                await workflow.ReplaceAsync(intake, token);
                return Results.Created("/intake/current", await workflow.AnalyseAndApplyAsync(intake, token));
            }));

        group.MapPost("/current", (IntakeSourceRequest request, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow, IntakeApplicationService application, TimeProvider clock) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                var id = request.IntakeId is { } requestedId && requestedId != Guid.Empty ? requestedId : Guid.NewGuid();
                var intake = new CurrentIntake { Id = id, UserId = userId, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
                if (request.SourceType?.Equals("url", StringComparison.OrdinalIgnoreCase) == true && Uri.TryCreate(request.Value, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" && url.AbsoluteUri.Length <= IntakeSourceIdentity.MaxUrlLength)
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
                intake.SourceFingerprint = IntakeSourceIdentity.Text(intake.SourceType == IntakeSourceType.Url ? "url" : "text", intake.Url ?? intake.Text!);
                var recovered = await RecoverAsync(id, userId, intake.SourceFingerprint, workflow, application, token);
                if (recovered is not null) return recovered;
                await workflow.ReplaceAsync(intake, token);
                return Results.Created("/intake/current", await workflow.AnalyseAndApplyAsync(intake, token));
            }));

        group.MapPost("/current/retry", (IntakeIdentityRequest request, HttpContext context, ICurrentUserAccessor currentUser, IntakeCommandLocks locks, IntakeWorkflowService workflow) =>
            CommandAsync(context, currentUser, locks, async (userId, token) =>
            {
                var intake = await workflow.FindAsync(userId, token);
                if (intake is null) return Results.NotFound();
                if (intake.Id != request.IntakeId) return Results.Conflict(new { message = "This upload was replaced. Reopen Add to Glovelly before retrying." });
                return Results.Ok(await workflow.AnalyseAndApplyAsync(intake, token));
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

    private static async Task<IResult?> RecoverAsync(Guid id, Guid userId, string fingerprint, IntakeWorkflowService workflow, IntakeApplicationService application, CancellationToken token)
    {
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
        return Results.Ok(current.AnalysisState == IntakeAnalysisState.Pending ? await workflow.AnalyseAndApplyAsync(current, token) : await workflow.CompleteAsync(current, token));
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

    private sealed record IntakeSourceRequest(string? SourceType, string? Value, Guid? IntakeId);
    private sealed record IntakeIdentityRequest(Guid IntakeId);
    private sealed record IntakeApplyRequest(Guid IntakeId, IntakeIntent Intent, Guid GigId, IntakeResourceInput? Resource);
}
