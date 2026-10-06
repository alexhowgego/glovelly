using System.Security.Cryptography;
using System.Text;

namespace Glovelly.Api.Services;

public static class IntakeSourceIdentity
{
    public const int MaxUrlLength = 2000;
    public static string File(string name, string contentType, byte[] content)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes($"file\0{name}\0{contentType.ToLowerInvariant()}\0"));
        hash.AppendData(content);
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static string Text(string type, string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{type}\0{value}")));
}
