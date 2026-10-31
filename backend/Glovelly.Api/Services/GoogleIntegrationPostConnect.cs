using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

internal static class GoogleIntegrationPostConnect
{
    internal static async Task CompleteAsync(GoogleIntegrationOAuth.Integration integration, Guid userId, Guid connectionId,
        AppDbContext db, IGoogleCalendarIntegrationService calendar, ICalendarSyncWorkQueue queue, CancellationToken token)
    {
        if (integration == GoogleIntegrationOAuth.Drive)
            await EnsureDriveSettingsAsync(db, userId, connectionId, token);
        else if (integration == GoogleIntegrationOAuth.Calendar)
        {
            await calendar.EnsureCalendarAsync(userId, token);
            await calendar.InvalidateSyncHashesAsync(userId, token);
            await queue.EnqueueFullSyncAsync(userId, CalendarSyncWorkItemReason.ConnectionChanged, token);
        }
    }

    private static async Task EnsureDriveSettingsAsync(AppDbContext db, Guid userId, Guid connectionId, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        var settings = await db.GoogleDriveIntegrationSettings.SingleOrDefaultAsync(value => value.UserId == userId, token);
        if (settings is null)
        {
            settings = new GoogleDriveIntegrationSettings { Id = Guid.NewGuid(), UserId = userId, CreatedAtUtc = now };
            db.GoogleDriveIntegrationSettings.Add(settings);
        }
        settings.GoogleConnectionId = connectionId;
        settings.UpdatedAtUtc = now;
        await db.SaveChangesAsync(token);
    }
}
