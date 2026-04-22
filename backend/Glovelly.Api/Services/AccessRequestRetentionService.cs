using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed class AccessRequestRetentionService(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<AccessRequestProtectionSettings> settings,
    ILogger<AccessRequestRetentionService> logger)
{
    private readonly AccessRequestProtectionSettings _settings = settings.Value;

    public async Task CleanupIfDueAsync(CancellationToken cancellationToken)
    {
        try
        {
            var now = timeProvider.GetUtcNow();
            var probeCutoffUtc = now - _settings.RetentionWindow - _settings.CleanupSlack;
            var cleanupIsDue = await dbContext.AccessRequests
                .AsNoTracking()
                .AnyAsync(value => value.RequestedAtUtc < probeCutoffUtc, cancellationToken);

            if (!cleanupIsDue)
            {
                return;
            }

            await CleanupExpiredRequestsAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Access request retention cleanup attempt failed.");
        }
    }

    internal async Task<int> CleanupExpiredRequestsAsync(CancellationToken cancellationToken)
    {
        var cutoffUtc = timeProvider.GetUtcNow() - _settings.RetentionWindow;

        logger.LogInformation(
            "Access request retention cleanup started with cutoff {CutoffUtc}.",
            cutoffUtc);

        try
        {
            var expiredRequests = await dbContext.AccessRequests
                .Where(value => value.RequestedAtUtc < cutoffUtc)
                .ToListAsync(cancellationToken);

            if (expiredRequests.Count == 0)
            {
                logger.LogInformation(
                    "Access request retention cleanup completed with cutoff {CutoffUtc}. DeletedRows: {DeletedRows}.",
                    cutoffUtc,
                    0);
                return 0;
            }

            dbContext.AccessRequests.RemoveRange(expiredRequests);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Access request retention cleanup completed with cutoff {CutoffUtc}. DeletedRows: {DeletedRows}.",
                cutoffUtc,
                expiredRequests.Count);

            return expiredRequests.Count;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Access request retention cleanup failed with cutoff {CutoffUtc}.",
                cutoffUtc);
            throw;
        }
    }
}
