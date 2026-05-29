using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Glovelly.Api.Services;

public sealed class CalendarEventPayloadHasher : ICalendarEventPayloadHasher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public string Hash(CalendarEventPayload payload)
    {
        var normalized = new
        {
            payload.SourceGigId,
            Summary = payload.Summary.Trim(),
            StartDate = payload.StartDate.ToString("O"),
            EndDate = payload.EndDate.ToString("O"),
            Location = string.IsNullOrWhiteSpace(payload.Location) ? null : payload.Location.Trim(),
            Description = payload.Description.Trim(),
        };
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
