using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed record StaticMapDescriptor(string Url, string SourceFingerprint, string Size);

public sealed class GoogleStaticMapsService(IOptions<GoogleStaticMapsSettings> options) : IDisposable
{
    private readonly PartitionedRateLimiter<string> rateLimiter = PartitionedRateLimiter.Create<string, string>(user =>
        RateLimitPartition.GetFixedWindowLimiter(user, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));

    public bool IsAvailable => options.Value.IsAvailable;

    public static string? Size(string bucket)
    {
        if (bucket == "small") return "200x300";
        if (bucket == "medium") return "280x400";
        var parts = bucket.Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height) ||
            width is < 32 or > 640 || height is < 32 or > 640 || bucket != $"{width}x{height}") return null;
        return bucket;
    }

    public StaticMapDescriptor? Create(string userId, string query, string bucket)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(query) || query.Length > 1500 || Size(bucket) is null) return null;
        if (!options.Value.TryGetSigningKey(out var secret)) return null;

        var parameters = new Dictionary<string, string?>
        {
            ["center"] = query, ["markers"] = query, ["zoom"] = "14",
            ["size"] = Size(bucket), ["scale"] = "1", ["format"] = "png",
            ["key"] = options.Value.ApiKey
        };
        var url = QueryHelpers.AddQueryString("https://maps.googleapis.com/maps/api/staticmap", parameters);
        if (url.Length > 16000) return null;
        using var permit = rateLimiter.AttemptAcquire(userId);
        if (!permit.IsAcquired) return null;

        var signature = SignPathAndQuery(new Uri(url).PathAndQuery, secret);
        return new(QueryHelpers.AddQueryString(url, "signature", signature), RecordLocationSource.Fingerprint(query), bucket);
    }

    public static string SignPathAndQuery(string pathAndQuery, byte[] key) =>
        WebEncoders.Base64UrlEncode(HMACSHA1.HashData(key, Encoding.UTF8.GetBytes(pathAndQuery)));

    public void Dispose() => rateLimiter.Dispose();
}
