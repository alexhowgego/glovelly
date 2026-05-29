using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glovelly.Api.Services;

public sealed class GoogleCalendarApiClient(HttpClient httpClient) : IGoogleCalendarApiClient
{
    private const string CalendarEndpoint = "https://www.googleapis.com/calendar/v3/calendars";
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
            throw new InvalidOperationException(
                $"Google Calendar creation failed with HTTP {(int)response.StatusCode}. {responseBody}".Trim());
        }

        var createResponse = JsonSerializer.Deserialize<GoogleCalendarCreateResponse>(responseBody, JsonOptions);
        if (createResponse is null || string.IsNullOrWhiteSpace(createResponse.Id))
        {
            throw new InvalidOperationException("Google Calendar creation response did not include a calendar id.");
        }

        return new GoogleCalendarCreateResult(createResponse.Id, createResponse.Summary ?? summary);
    }

    private sealed class GoogleCalendarCreateResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }
}
