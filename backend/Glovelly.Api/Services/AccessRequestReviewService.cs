using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed class AccessRequestReviewService(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<AccessRequestProtectionSettings> settings)
{
    // EF's optimistic concurrency handles cross-instance decisions; this closes the in-process gap too.
    private static readonly SemaphoreSlim DecisionLock = new(1, 1);
    private readonly AccessRequestProtectionSettings _settings = settings.Value;

    public async Task<IReadOnlyList<AccessRequest>> ListPendingAsync(CancellationToken cancellationToken)
    {
        await ExpirePendingAsync(cancellationToken);
        return await dbContext.AccessRequests.AsNoTracking()
            .Where(request => request.Status == AccessRequestStatus.Pending)
            .OrderBy(request => request.RequestedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<AccessRequest?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await ExpirePendingAsync(cancellationToken);
        return await dbContext.AccessRequests.AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

    public async Task<AccessRequestDecisionResult> ApproveAsync(
        Guid id, Guid reviewerId, UserRole role, bool isActive, string? note, CancellationToken cancellationToken)
    {
        await DecisionLock.WaitAsync(cancellationToken);
        try
        {
            return await ApproveCoreAsync(id, reviewerId, role, isActive, note, cancellationToken);
        }
        finally
        {
            DecisionLock.Release();
        }
    }

    private async Task<AccessRequestDecisionResult> ApproveCoreAsync(
        Guid id, Guid reviewerId, UserRole role, bool isActive, string? note, CancellationToken cancellationToken)
    {
        var request = await dbContext.AccessRequests.FirstOrDefaultAsync(value => value.Id == id, cancellationToken);
        await ExpireIfStaleAsync(request, cancellationToken);
        if (request is null || request.Status != AccessRequestStatus.Pending)
        {
            return new AccessRequestDecisionResult(request, false, false, false);
        }

        var existingUser = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == request.NormalizedEmail, cancellationToken);
        var user = existingUser ?? new User
        {
            Id = Guid.NewGuid(),
            Email = request.NormalizedEmail,
            DisplayName = request.DisplayName,
            Role = role,
            IsActive = isActive,
            CreatedUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        request.Status = AccessRequestStatus.Provisioned;
        request.DecisionAtUtc = timeProvider.GetUtcNow();
        request.ReviewedByUserId = reviewerId;
        request.ProvisionedUserId = user.Id;
        request.DecisionNote = NormalizeNote(note);
        if (existingUser is null)
        {
            dbContext.Users.Add(user);
        }
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return new AccessRequestDecisionResult(await GetAsync(id, cancellationToken), false, false, false);
        }
        catch (DbUpdateException) when (existingUser is null)
        {
            // A simultaneous manual provision may have claimed the unique email.
            dbContext.ChangeTracker.Clear();
            existingUser = await dbContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(value => value.Email == request.NormalizedEmail, cancellationToken);
            return new AccessRequestDecisionResult(await GetAsync(id, cancellationToken), false, existingUser is not null, false);
        }

        return new AccessRequestDecisionResult(await GetAsync(id, cancellationToken), existingUser is null, existingUser is not null, true);
    }

    public async Task<AccessRequestDecisionResult> DeclineAsync(
        Guid id, Guid reviewerId, string? note, CancellationToken cancellationToken)
    {
        await DecisionLock.WaitAsync(cancellationToken);
        try
        {
            return await DeclineCoreAsync(id, reviewerId, note, cancellationToken);
        }
        finally
        {
            DecisionLock.Release();
        }
    }

    private async Task<AccessRequestDecisionResult> DeclineCoreAsync(
        Guid id, Guid reviewerId, string? note, CancellationToken cancellationToken)
    {
        var request = await dbContext.AccessRequests.FirstOrDefaultAsync(value => value.Id == id, cancellationToken);
        await ExpireIfStaleAsync(request, cancellationToken);
        if (request is null || request.Status != AccessRequestStatus.Pending)
        {
            return new AccessRequestDecisionResult(request, false, false, false);
        }
        request.Status = AccessRequestStatus.Declined;
        request.DecisionAtUtc = timeProvider.GetUtcNow();
        request.ReviewedByUserId = reviewerId;
        request.DecisionNote = NormalizeNote(note);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new AccessRequestDecisionResult(request, false, false, true);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return new AccessRequestDecisionResult(await GetAsync(id, cancellationToken), false, false, false);
        }
    }

    private async Task<int> ExpirePendingAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var requests = await dbContext.AccessRequests
            .Where(value => value.Status == AccessRequestStatus.Pending && value.RequestedAtUtc <= now - _settings.ApprovalWindow)
            .ToListAsync(cancellationToken);
        foreach (var request in requests)
        {
            request.Status = AccessRequestStatus.Expired;
            request.DecisionAtUtc = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return requests.Count;
    }

    private async Task ExpireIfStaleAsync(AccessRequest? request, CancellationToken cancellationToken)
    {
        if (request?.Status == AccessRequestStatus.Pending && request.RequestedAtUtc <= timeProvider.GetUtcNow() - _settings.ApprovalWindow)
        {
            request.Status = AccessRequestStatus.Expired;
            request.DecisionAtUtc = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}

public sealed record AccessRequestDecisionResult(
    AccessRequest? AccessRequest,
    bool UserCreated,
    bool ExistingUser,
    bool DecisionApplied);
