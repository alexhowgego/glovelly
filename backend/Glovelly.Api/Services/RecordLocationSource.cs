using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

/// <summary>Versioned query identity shared with the browser; never includes client/contact identity.</summary>
public static partial class RecordLocationSource
{
    public static string ClientQuery(Address? address) => address is null ? string.Empty :
        string.Join(", ", new[] { address.Line1, address.Line2, address.City, address.StateOrCounty, address.PostalCode, address.Country }
            .Select(Normalize).Where(value => value.Length > 0));

    public static string GigQuery(string? venue) => Normalize(venue);

    public static string Fingerprint(string query) => "v1:" +
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(query)));

    // Explicit ASCII whitespace rather than runtime-dependent Unicode \s sets.
    private static string Normalize(string? value) => Whitespace().Replace(value ?? string.Empty, " ").Trim(' ');

    [GeneratedRegex("[ \\t\\r\\n\\f\\v]+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
