using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public static class InvoiceDescriptionBuilder
{
    public static string ForGig(Gig gig)
    {
        return $"In respect of {gig.Title} at {gig.Venue} on {gig.Date:yyyy-MM-dd}.";
    }
}
