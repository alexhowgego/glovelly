using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class IntakePreparationWorker(IServiceScopeFactory scopes, ILogger<IntakePreparationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Unable to scan gig intake preparation jobs; retrying."); }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    public async Task<bool> DrainOnceAsync(CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var analyses = await db.CurrentIntakes.AsNoTracking().Where(value => value.AnalysisInBackground)
            .Select(value => new { value.Id, value.UserId }).Take(20).ToListAsync(token);
        foreach (var item in analyses) await ProcessAnalysisAsync(item.Id, item.UserId, token);
        var stale = DateTimeOffset.UtcNow.AddMinutes(-2);
        var pending = await db.CurrentIntakes.AsNoTracking()
            .Where(value => value.PreparationState == IntakePreparationState.Pending || value.PreparationState == IntakePreparationState.Running && value.PreparationStartedAt < stale)
            .Select(value => new { value.Id, value.UserId, value.PreparationId }).Take(20).ToListAsync(token);
        foreach (var item in pending)
            if (item.PreparationId.HasValue) await ProcessAsync(item.Id, item.UserId, item.PreparationId.Value, token);
        return await db.CurrentIntakes.AsNoTracking().AnyAsync(value => value.AnalysisInBackground || value.PreparationState == IntakePreparationState.Pending || value.PreparationState == IntakePreparationState.Running, token);
    }

    private async Task ProcessAnalysisAsync(Guid intakeId, Guid userId, CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var gate = services.GetRequiredService<IntakeCommandLocks>().ForUser(userId);
        await gate.WaitAsync(token);
        try
        {
            await using var commandScope = new IntakeCommandScope(db);
            await commandScope.AcquireAsync(userId, token);
            var workflow = services.GetRequiredService<IntakeWorkflowService>();
            var intake = await workflow.FindAsync(userId, token);
            if (intake is null || intake.Id != intakeId || !intake.AnalysisInBackground) return;
            try
            {
                if (intake.AnalysisState == IntakeAnalysisState.Pending) await workflow.AnalyseAndApplyAsync(intake, token);
                else if (intake.AnalysisState == IntakeAnalysisState.Succeeded) await workflow.CompleteAsync(intake, token);
                db.ChangeTracker.Clear();
                intake = await workflow.FindAsync(userId, token);
                if (intake is not null && intake.Id == intakeId) { intake.AnalysisInBackground = false; await db.SaveChangesAsync(token); }
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                db.ChangeTracker.Clear();
                intake = await workflow.FindAsync(userId, token);
                if (intake is not null && intake.Id == intakeId)
                {
                    intake.AnalysisInBackground = false;
                    intake.AnalysisState = IntakeAnalysisState.Failed;
                    intake.FailureMessage = "Analysis could not be completed. Retry or choose how to treat this source.";
                    await db.SaveChangesAsync(token);
                }
            }
        }
        finally { gate.Release(); }
    }

    private async Task ProcessAsync(Guid intakeId, Guid userId, Guid preparationId, CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var gate = services.GetRequiredService<IntakeCommandLocks>().ForUser(userId);
        await gate.WaitAsync(token);
        try
        {
            await using var commandScope = new IntakeCommandScope(db);
            await commandScope.AcquireAsync(userId, token);
            var intake = await db.CurrentIntakes.SingleOrDefaultAsync(value => value.Id == intakeId && value.UserId == userId, token);
            if (intake is null || intake.PreparationId != preparationId || intake.IntentOverride != IntakeIntent.Gigs ||
                (intake.PreparationState != IntakePreparationState.Pending && (intake.PreparationState != IntakePreparationState.Running || intake.PreparationStartedAt >= DateTimeOffset.UtcNow.AddMinutes(-2)))) return;
            intake.PreparationState = IntakePreparationState.Running;
            intake.PreparationStartedAt = DateTimeOffset.UtcNow;
            intake.PreparationError = null;
            await db.SaveChangesAsync(token);
            try
            {
                await services.GetRequiredService<IntakeGigDraftService>().PrepareAsync(intakeId, userId, false, token);
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                var failureCode = error is GigPreparationException known ? known.Code : "unexpected_failure";
                logger.LogWarning("Gig preparation failed for intake {IntakeId}, preparation {PreparationId}: {FailureCode} ({ErrorType}).", intakeId, preparationId, failureCode, error.GetType().Name);
                db.ChangeTracker.Clear();
                intake = await db.CurrentIntakes.SingleOrDefaultAsync(value => value.Id == intakeId && value.UserId == userId && value.PreparationId == preparationId, token);
                if (intake is not null)
                {
                    intake.PreparationState = IntakePreparationState.Failed;
                    intake.PreparationError = error is GigPreparationException safe ? safe.Message : "Gig preparation failed unexpectedly. Your source is retained; retry or prepare a manual draft.";
                    await db.SaveChangesAsync(token);
                }
            }
        }
        finally { gate.Release(); }
    }
}
