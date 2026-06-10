using Google;
using System.Net;

namespace Glovelly.Api.Services;

internal static class GoogleApiExceptionSupport
{
    public static bool IsNotFound(GoogleApiException exception)
    {
        return exception.HttpStatusCode == HttpStatusCode.NotFound ||
            exception.Error?.Code == StatusCodes.Status404NotFound;
    }
}
