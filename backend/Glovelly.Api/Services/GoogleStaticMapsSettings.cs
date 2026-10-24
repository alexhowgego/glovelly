namespace Glovelly.Api.Services;

public sealed class GoogleStaticMapsSettings
{
    public const string SectionName = "Maps:GoogleStatic";
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
    public bool IsAvailable => Enabled && ApiKey.Length is > 0 and <= 200 &&
        ApiKey.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') && TryGetSigningKey(out _);

    public bool TryGetSigningKey(out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(SigningSecret) || SigningSecret.Length > 256) return false;
        try
        {
            var encoded = SigningSecret.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            key = Convert.FromBase64String(encoded);
            return key.Length is >= 16 and <= 128;
        }
        catch (FormatException) { return false; }
    }
}
