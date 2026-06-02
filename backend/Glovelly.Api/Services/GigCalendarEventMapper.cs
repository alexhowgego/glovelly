using System.Globalization;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed class GigCalendarEventMapper : IGigCalendarEventMapper
{
    public bool ShouldExistInCalendar(Gig gig, GoogleCalendarIntegrationSettings settings)
    {
        if (!settings.IsEnabled || settings.DisconnectedAtUtc is not null)
        {
            return false;
        }

        return gig.Status is GigStatus.Confirmed or GigStatus.Completed;
    }

    public CalendarEventPayload Map(
        Gig gig,
        Client client,
        GoogleCalendarIntegrationSettings settings)
    {
        var descriptionLines = new List<string>
        {
            $"Client: {client.Name}",
            $"Status: {gig.Status}",
            $"Fee: {gig.Fee.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"Glovelly gig ID: {gig.Id}",
        };

        if (!string.IsNullOrWhiteSpace(gig.Notes))
        {
            descriptionLines.Add(string.Empty);
            descriptionLines.Add(gig.Notes.Trim());
        }

        return new CalendarEventPayload(
            gig.Id,
            gig.Title.Trim(),
            gig.Date,
            gig.Date.AddDays(1),
            settings.IncludeLocation ? gig.Venue.Trim() : null,
            string.Join(Environment.NewLine, descriptionLines));
    }
}
