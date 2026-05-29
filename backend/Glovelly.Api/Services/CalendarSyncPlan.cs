using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed record CalendarSyncPlan(
    CalendarSyncOperation Operation,
    CalendarProvider Provider,
    CalendarEventPayload? Payload,
    string? PayloadHash,
    GigCalendarSyncState? SyncState);
