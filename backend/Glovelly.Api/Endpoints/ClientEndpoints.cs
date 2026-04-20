using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

public static class ClientEndpoints
{
    public static RouteGroupBuilder MapClientEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var clients = await db.Clients
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .OrderBy(client => client.Name)
                .ToListAsync();

            return Results.Ok(clients);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var client = await db.Clients
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .FirstOrDefaultAsync(client => client.Id == id);

            return client is null ? Results.NotFound() : Results.Ok(client);
        });

        group.MapPost("/", async (Client client, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var pricingValidation = EndpointSupport.ValidateClientPricing(client);
            if (pricingValidation is not null)
            {
                return pricingValidation;
            }

            client.Id = Guid.NewGuid();
            client.Name = client.Name.Trim();
            client.Email = client.Email.Trim();
            client.BillingAddress ??= new Address();
            EndpointSupport.StampCreate(client, currentUserAccessor.TryGetUserId(user));

            db.Clients.Add(client);
            await db.SaveChangesAsync();

            return Results.Created($"/clients/{client.Id}", client);
        });

        group.MapPut("/{id:guid}", async (Guid id, Client request, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var client = await db.Clients
                .WhereVisibleTo(userId)
                .FirstOrDefaultAsync(client => client.Id == id);
            if (client is null)
            {
                return Results.NotFound();
            }

            var pricingValidation = EndpointSupport.ValidateClientPricing(request);
            if (pricingValidation is not null)
            {
                return pricingValidation;
            }

            client.Name = request.Name.Trim();
            client.Email = request.Email.Trim();
            client.BillingAddress = request.BillingAddress ?? new Address();
            client.MileageRate = request.MileageRate;
            client.PassengerMileageRate = request.PassengerMileageRate;
            EndpointSupport.StampUpdate(client, userId);

            await db.SaveChangesAsync();

            return Results.Ok(client);
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var client = await db.Clients
                .WhereVisibleTo(userId)
                .FirstOrDefaultAsync(client => client.Id == id);
            if (client is null)
            {
                return Results.NotFound();
            }

            db.Clients.Remove(client);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return group;
    }
}
