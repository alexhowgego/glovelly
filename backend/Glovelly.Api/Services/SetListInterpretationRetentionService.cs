using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class SetListInterpretationRetentionService(AppDbContext db, TimeProvider clock)
{
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        var expired = await db.SetListInterpretationJobs.Where(job => job.SourceGridExpiresAtUtc < clock.GetUtcNow() && job.SourceGridJson != "{}").ToListAsync(cancellationToken);
        foreach (var job in expired) job.SourceGridJson = "{}";
        if (expired.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
