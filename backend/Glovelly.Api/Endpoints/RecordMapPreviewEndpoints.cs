using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class RecordMapPreviewEndpoints
{
    public static void MapRecordMapPreview(this RouteGroupBuilder group, bool isClient)
    {
        group.MapGet("/{id:guid}/map-preview", async (Guid id, string source, string size, AppDbContext db,
            ClaimsPrincipal user, ICurrentUserAccessor currentUser, GoogleStaticMapsService maps,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            var userId = currentUser.TryGetUserId(user);
            if (GoogleStaticMapsService.Size(size) is null || source.Length != 67 || !source.StartsWith("v1:", StringComparison.Ordinal))
                return Results.BadRequest();

            if (context.Request.Query.Keys.Any(key => key is not ("source" or "size")) ||
                context.Request.Query["source"].Count != 1 || context.Request.Query["size"].Count != 1 ||
                !source.AsSpan(3).ContainsOnlyHexDigits()) return Results.BadRequest();

            async Task<string?> ReadSource(CancellationToken token)
            {
                if (isClient)
                {
                    var record = await db.Clients.WhereVisibleTo(userId).AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, token);
                    return record is null ? null : RecordLocationSource.ClientQuery(record.BillingAddress);
                }
                else
                {
                    var record = await db.Gigs.WhereVisibleTo(userId).AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, token);
                    return record is null ? null : RecordLocationSource.GigQuery(record.Venue);
                }
            }

            var location = await ReadSource(cancellationToken);
            if (location is null) return Results.NotFound();
            if (RecordLocationSource.Fingerprint(location) != source) return Results.Conflict();
            if (!maps.IsAvailable || !userId.HasValue) return Results.NoContent();
            var descriptor = maps.Create(userId.Value.ToString(), location, size);
            return descriptor is null ? Results.NoContent() : Results.Ok(descriptor);
        });
    }

    private static bool ContainsOnlyHexDigits(this ReadOnlySpan<char> value)
    {
        foreach (var character in value)
            if (!char.IsAsciiHexDigit(character)) return false;
        return true;
    }
}
