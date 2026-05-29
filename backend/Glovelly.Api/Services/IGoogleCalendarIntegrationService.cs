using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IGoogleCalendarIntegrationService
{
    Task<GoogleCalendarIntegrationSettings> EnsureCalendarAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
