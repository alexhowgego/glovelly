namespace Glovelly.Api.Services;

public sealed record CalendarEventPayload(
    Guid SourceGigId,
    string Summary,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Location,
    string Description);
