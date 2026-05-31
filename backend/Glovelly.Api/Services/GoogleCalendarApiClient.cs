using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glovelly.Api.Services;

public sealed class GoogleCalendarApiClient(HttpClient httpClient) : IGoogleCalendarApiClient
{
    private const string CalendarEndpoint = "https://www.googleapis.com/calendar/v3/calendars";
    private const string EventsEndpoint = "https://www.googleapis.com/calendar/v3/calendars/{0}/events";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GoogleCalendarCreateResult> CreateCalendarAsync(
        string accessToken,
        string summary,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, CalendarEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { summary }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw BuildException("Google Calendar creation", response.StatusCode, responseBody);
        }

        var createResponse = JsonSerializer.Deserialize<GoogleCalendarCreateResponse>(responseBody, JsonOptions);
        if (createResponse is null || string.IsNullOrWhiteSpace(createResponse.Id))
        {
            throw new InvalidOperationException("Google Calendar creation response did not include a calendar id.");
        }

        return new GoogleCalendarCreateResult(createResponse.Id, createResponse.Summary ?? summary);
    }

    public async Task<GoogleCalendarEventResult> CreateEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        CalendarEventPayload payload,
        CancellationToken cancellationToken)
    {
        var endpoint = string.Format(EventsEndpoint, Uri.EscapeDataString(calendarId));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = BuildEventContent(eventId, payload);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return await UpdateEventAsync(accessToken, calendarId, eventId, payload, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw BuildException("Google Calendar event creation", response.StatusCode, responseBody);
        }

        return ParseEventResponse(responseBody, eventId);
    }

    public async Task<GoogleCalendarEventResult> UpdateEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        CalendarEventPayload payload,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{string.Format(EventsEndpoint, Uri.EscapeDataString(calendarId))}/{Uri.EscapeDataString(eventId)}";
        using var request = new HttpRequestMessage(HttpMethod.Put, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = BuildEventContent(eventId, payload);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw BuildException("Google Calendar event update", response.StatusCode, responseBody);
        }

        return ParseEventResponse(responseBody, eventId);
    }

    public async Task DeleteEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{string.Format(EventsEndpoint, Uri.EscapeDataString(calendarId))}/{Uri.EscapeDataString(eventId)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
            response.StatusCode == System.Net.HttpStatusCode.Gone)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw BuildException("Google Calendar event deletion", response.StatusCode, responseBody);
        }
    }

    public static string BuildDeterministicEventId(Guid gigId)
    {
        return $"glv{gigId:N}";
    }

    private static StringContent BuildEventContent(string eventId, CalendarEventPayload payload)
    {
        var value = new
        {
            id = eventId,
            summary = payload.Summary,
            location = payload.Location,
            description = payload.Description,
            start = new { date = payload.StartDate.ToString("O") },
            end = new { date = payload.EndDate.ToString("O") },
            extendedProperties = new
            {
                @private = new Dictionary<string, string>
                {
                    ["glovellyGigId"] = payload.SourceGigId.ToString(),
                }
            }
        };

        return new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");
    }

    private static GoogleCalendarEventResult ParseEventResponse(string responseBody, string fallbackId)
    {
        var eventResponse = JsonSerializer.Deserialize<GoogleCalendarEventResponse>(responseBody, JsonOptions);
        return new GoogleCalendarEventResult(
            string.IsNullOrWhiteSpace(eventResponse?.Id) ? fallbackId : eventResponse.Id);
    }

    private static GoogleCalendarApiException BuildException(
        string operation,
        System.Net.HttpStatusCode statusCode,
        string responseBody)
    {
        return new GoogleCalendarApiException(
            $"{operation} failed with HTTP {(int)statusCode}. {responseBody}".Trim(),
            statusCode,
            responseBody);
    }

    private sealed class GoogleCalendarCreateResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }

    private sealed class GoogleCalendarEventResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
