using System.Net;

namespace Glovelly.Api.Services;

public sealed class GoogleCalendarApiException(
    string message,
    HttpStatusCode statusCode,
    string responseBody) : InvalidOperationException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = responseBody;
}
