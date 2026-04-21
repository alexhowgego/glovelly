using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Auth;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

public static class InvoiceLineEndpoints
{
    public static RouteGroupBuilder MapInvoiceLineEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var lines = await db.InvoiceLines
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .OrderBy(line => line.InvoiceId)
                .ThenBy(line => line.SortOrder)
                .ThenBy(line => line.Description)
                .ToListAsync();

            return Results.Ok(lines);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var line = await db.InvoiceLines
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .FirstOrDefaultAsync(line => line.Id == id);

            return line is null ? Results.NotFound() : Results.Ok(line);
        });

        group.MapPost("/", async (InvoiceLine line, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == line.InvoiceId);

            if (invoice is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["invoiceId"] = ["Invoice does not exist."]
                });
            }

            var gigValidation = await EndpointSupport.ValidateInvoiceLineGigAsync(invoice, line.GigId, db);
            if (gigValidation is not null)
            {
                return gigValidation;
            }

            line.Id = Guid.NewGuid();
            line.Description = line.Description.Trim();
            line.CalculationNotes = line.CalculationNotes?.Trim();
            line.Invoice = null;
            line.Gig = null;
            EndpointSupport.StampCreate(line, userId);

            db.InvoiceLines.Add(line);
            await db.SaveChangesAsync();

            return Results.Created($"/invoice-lines/{line.Id}", line);
        });

        group.MapPut("/{id:guid}", async (Guid id, InvoiceLine request, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var line = await db.InvoiceLines
                .WhereVisibleTo(userId)
                .FirstOrDefaultAsync(value => value.Id == id);
            if (line is null)
            {
                return Results.NotFound();
            }

            var invoice = await db.Invoices
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == request.InvoiceId);

            if (invoice is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["invoiceId"] = ["Invoice does not exist."]
                });
            }

            var gigValidation = await EndpointSupport.ValidateInvoiceLineGigAsync(invoice, request.GigId, db);
            if (gigValidation is not null)
            {
                return gigValidation;
            }

            line.InvoiceId = request.InvoiceId;
            line.SortOrder = request.SortOrder;
            line.Type = request.Type;
            line.Description = request.Description.Trim();
            line.Quantity = request.Quantity;
            line.UnitPrice = request.UnitPrice;
            line.GigId = request.GigId;
            line.CalculationNotes = request.CalculationNotes?.Trim();

            await db.SaveChangesAsync();

            return Results.Ok(line);
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var line = await db.InvoiceLines
                .WhereVisibleTo(userId)
                .FirstOrDefaultAsync(value => value.Id == id);
            if (line is null)
            {
                return Results.NotFound();
            }

            db.InvoiceLines.Remove(line);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return group;
    }
}
