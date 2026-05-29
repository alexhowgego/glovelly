namespace Glovelly.Api.Services;

public interface IGoogleCalendarApiClient
{
    Task<GoogleCalendarCreateResult> CreateCalendarAsync(
        string accessToken,
        string summary,
        CancellationToken cancellationToken);
}
