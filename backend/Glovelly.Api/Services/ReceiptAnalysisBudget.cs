using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

// The provider budget is shared by both analysis adapters; acquisition and
// metadata/recovery requests do not consume expensive-analysis permits.
public sealed class ReceiptAnalysisBudget : IDisposable
{
    private readonly PartitionedRateLimiter<Guid> _limiter;
    public ReceiptAnalysisBudget(IOptions<ReceiptAnalysisSettings> options)
    {
        var settings = options.Value;
        _limiter = PartitionedRateLimiter.Create<Guid, Guid>(userId => RateLimitPartition.GetFixedWindowLimiter(userId,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, settings.PerUserPermitLimit), Window = settings.PerUserWindow, QueueLimit = 0, AutoReplenishment = true }));
    }
    public RateLimitLease Acquire(Guid userId) => _limiter.AttemptAcquire(userId);
    public void Dispose() => _limiter.Dispose();
}
