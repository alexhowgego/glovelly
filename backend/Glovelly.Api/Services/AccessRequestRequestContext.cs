using Microsoft.AspNetCore.Http;
using System.Net;

namespace Glovelly.Api.Services;

internal static class AccessRequestRequestContext
{
    private const string TestRemoteIpHeader = "X-Test-Remote-Ip";

    public static IPAddress? ResolveRemoteIpAddress(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(TestRemoteIpHeader, out var values)
            && IPAddress.TryParse(values.FirstOrDefault(), out var parsed))
        {
            return parsed;
        }

        return httpContext.Connection.RemoteIpAddress;
    }

    public static string ResolveRateLimitPartitionKey(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(TestRemoteIpHeader, out var values))
        {
            var testIp = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(testIp))
            {
                return testIp.Trim();
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
