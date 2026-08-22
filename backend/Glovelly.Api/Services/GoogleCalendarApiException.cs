using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glovelly.Api.Services;

public sealed class GoogleCalendarApiException(
    string message,
    HttpStatusCode statusCode,
    string responseBody) : InvalidOperationException(message)
{
    public const string InsufficientScopeMessage = "Glovelly no longer has permission to add events to this Google Calendar. Reconnect your calendar to grant the required access.";
    public const string GenericCalendarErrorMessage = "Google Calendar could not be updated. Please try again later.";

    public HttpStatusCode StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = responseBody;

    public bool IsInsufficientScope =>
        StatusCode == HttpStatusCode.Forbidden && ContainsInsufficientScopeReason(ResponseBody);

    private static bool ContainsInsufficientScopeReason(string responseBody)
    {
        try
        {
            var response = JsonSerializer.Deserialize<GoogleCalendarErrorResponse>(responseBody);
            var error = response?.Error;
            if (error is null || (error.Code != 0 && error.Code != (int)HttpStatusCode.Forbidden))
            {
                return false;
            }

            return error.Errors.Concat(error.Details).Any(error =>
                string.Equals(error.Reason, "ACCESS_TOKEN_SCOPE_INSUFFICIENT", StringComparison.Ordinal) ||
                string.Equals(error.Reason, "insufficientPermissions", StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class GoogleCalendarErrorResponse
    {
        [JsonPropertyName("error")]
        public GoogleCalendarError? Error { get; init; }
    }

    private sealed class GoogleCalendarError
    {
        [JsonPropertyName("code")]
        public int Code { get; init; }

        [JsonPropertyName("errors")]
        public IReadOnlyList<GoogleCalendarErrorReason> Errors { get; init; } = [];

        [JsonPropertyName("details")]
        public IReadOnlyList<GoogleCalendarErrorReason> Details { get; init; } = [];
    }

    private sealed class GoogleCalendarErrorReason
    {
        [JsonPropertyName("reason")]
        public string? Reason { get; init; }
    }
}
