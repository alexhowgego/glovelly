using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

public static class CrudEndpoints
{
    public static IEndpointRouteBuilder MapCrudEndpoints(this IEndpointRouteBuilder app)
    {
        var clients = app.MapGroup("/clients").WithTags("Clients").RequireAuthorization(GlovellyPolicies.GlovellyUser);
        var gigs = app.MapGroup("/gigs").WithTags("Gigs").RequireAuthorization(GlovellyPolicies.GlovellyUser);
        var invoices = app.MapGroup("/invoices").WithTags("Invoices").RequireAuthorization(GlovellyPolicies.GlovellyUser);
        var invoiceLines = app.MapGroup("/invoice-lines").WithTags("InvoiceLines").RequireAuthorization(GlovellyPolicies.GlovellyUser);

        MapClientEndpoints(clients);
        MapGigEndpoints(gigs);
        MapInvoiceEndpoints(invoices);
        MapInvoiceLineEndpoints(invoiceLines);

        return app;
    }

    private static void MapClientEndpoints(RouteGroupBuilder group)
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
            client.Id = Guid.NewGuid();
            client.Name = client.Name.Trim();
            client.Email = client.Email.Trim();
            client.BillingAddress ??= new Address();
            StampCreate(client, currentUserAccessor.TryGetUserId(user));

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

            client.Name = request.Name.Trim();
            client.Email = request.Email.Trim();
            client.BillingAddress = request.BillingAddress ?? new Address();
            StampUpdate(client, currentUserAccessor.TryGetUserId(user));

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
    }

    private static void MapGigEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db) =>
        {
            var gigs = await db.Gigs
                .AsNoTracking()
                .OrderBy(gig => gig.Date)
                .ThenBy(gig => gig.Title)
                .ToListAsync();

            return Results.Ok(gigs);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var gig = await db.Gigs
                .AsNoTracking()
                .FirstOrDefaultAsync(gig => gig.Id == id);

            return gig is null ? Results.NotFound() : Results.Ok(gig);
        });

        group.MapPost("/", async (Gig gig, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == gig.ClientId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            if (gig.InvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == gig.InvoiceId.Value);

                if (invoice is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice does not exist."]
                    });
                }

                if (invoice.ClientId != gig.ClientId)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice client must match the gig client."]
                    });
                }
            }

            gig.Id = Guid.NewGuid();
            gig.Title = gig.Title.Trim();
            gig.Venue = gig.Venue.Trim();
            gig.Notes = gig.Notes?.Trim();
            gig.Client = null;
            gig.Invoice = null;
            gig.InvoicedAt = ResolveInvoicedAt(gig.InvoiceId, null, null, gig.InvoicedAt);
            StampCreate(gig, currentUserAccessor.TryGetUserId(user));

            db.Gigs.Add(gig);
            await db.SaveChangesAsync();

            return Results.Created($"/gigs/{gig.Id}", gig);
        });

        group.MapPut("/{id:guid}", async (Guid id, Gig request, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs.FirstOrDefaultAsync(value => value.Id == id);

            if (gig is null)
            {
                return Results.NotFound();
            }

            if (!await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == request.ClientId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            if (request.InvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == request.InvoiceId.Value);

                if (invoice is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice does not exist."]
                    });
                }

                if (invoice.ClientId != request.ClientId)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice client must match the gig client."]
                    });
                }
            }

            var previousInvoiceId = gig.InvoiceId;

            gig.ClientId = request.ClientId;
            gig.InvoiceId = request.InvoiceId;
            gig.Title = request.Title.Trim();
            gig.Date = request.Date;
            gig.Venue = request.Venue.Trim();
            gig.Fee = request.Fee;
            gig.TravelMiles = request.TravelMiles;
            gig.Notes = request.Notes?.Trim();
            gig.WasDriving = request.WasDriving;
            gig.Status = request.Status;
            gig.InvoicedAt = ResolveInvoicedAt(request.InvoiceId, previousInvoiceId, gig.InvoicedAt, request.InvoicedAt);
            StampUpdate(gig, currentUserAccessor.TryGetUserId(user));

            await db.SaveChangesAsync();

            return Results.Ok(gig);
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var gig = await db.Gigs.FirstOrDefaultAsync(gig => gig.Id == id);
            if (gig is null)
            {
                return Results.NotFound();
            }

            db.Gigs.Remove(gig);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }

    private static void MapInvoiceEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db) =>
        {
            var invoices = await db.Invoices
                .AsNoTracking()
                .Include(invoice => invoice.Lines)
                .OrderByDescending(invoice => invoice.InvoiceDate)
                .ThenBy(invoice => invoice.InvoiceNumber)
                .ToListAsync();

            return Results.Ok(invoices);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var invoice = await db.Invoices
                .AsNoTracking()
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);

            return invoice is null ? Results.NotFound() : Results.Ok(invoice);
        });

        group.MapPost("/", async (Invoice invoice, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == invoice.ClientId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            invoice.Id = Guid.NewGuid();
            invoice.InvoiceNumber = invoice.InvoiceNumber.Trim();
            invoice.Description = invoice.Description?.Trim();
            invoice.Client = null;
            invoice.Lines = new List<InvoiceLine>();
            StampCreate(invoice, currentUserAccessor.TryGetUserId(user));

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            return Results.Created($"/invoices/{invoice.Id}", invoice);
        });

        group.MapPut("/{id:guid}", async (Guid id, Invoice request, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var invoice = await db.Invoices
                .Include(value => value.Lines)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (invoice is null)
            {
                return Results.NotFound();
            }

            if (!await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == request.ClientId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            var hasConflictingGigLinks = await db.InvoiceLines
                .Where(line => line.InvoiceId == invoice.Id && line.GigId.HasValue)
                .Join(
                    db.Gigs,
                    line => line.GigId!.Value,
                    gig => gig.Id,
                    (line, gig) => gig.ClientId)
                .AnyAsync(clientId => clientId != request.ClientId);

            if (hasConflictingGigLinks)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Invoice client must match any linked gig line clients."]
                });
            }

            invoice.InvoiceNumber = request.InvoiceNumber.Trim();
            invoice.ClientId = request.ClientId;
            invoice.InvoiceDate = request.InvoiceDate;
            invoice.DueDate = request.DueDate;
            invoice.Status = request.Status;
            invoice.Description = request.Description?.Trim();
            invoice.PdfBlob = request.PdfBlob;
            StampUpdate(invoice, currentUserAccessor.TryGetUserId(user));

            await db.SaveChangesAsync();

            return Results.Ok(invoice);
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var invoice = await db.Invoices.FirstOrDefaultAsync(invoice => invoice.Id == id);
            if (invoice is null)
            {
                return Results.NotFound();
            }

            db.Invoices.Remove(invoice);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }

    private static void MapInvoiceLineEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db) =>
        {
            var lines = await db.InvoiceLines
                .AsNoTracking()
                .OrderBy(line => line.InvoiceId)
                .ThenBy(line => line.SortOrder)
                .ThenBy(line => line.Description)
                .ToListAsync();

            return Results.Ok(lines);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var line = await db.InvoiceLines
                .AsNoTracking()
                .FirstOrDefaultAsync(line => line.Id == id);

            return line is null ? Results.NotFound() : Results.Ok(line);
        });

        group.MapPost("/", async (InvoiceLine line, AppDbContext db) =>
        {
            var invoice = await db.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == line.InvoiceId);

            if (invoice is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["invoiceId"] = ["Invoice does not exist."]
                });
            }

            var gigValidation = await ValidateInvoiceLineGigAsync(invoice, line.GigId, db);
            if (gigValidation is not null)
            {
                return gigValidation;
            }

            line.Id = Guid.NewGuid();
            line.Description = line.Description.Trim();
            line.CalculationNotes = line.CalculationNotes?.Trim();
            line.Invoice = null;
            line.Gig = null;

            db.InvoiceLines.Add(line);
            await db.SaveChangesAsync();

            return Results.Created($"/invoice-lines/{line.Id}", line);
        });

        group.MapPut("/{id:guid}", async (Guid id, InvoiceLine request, AppDbContext db) =>
        {
            var line = await db.InvoiceLines.FirstOrDefaultAsync(value => value.Id == id);
            if (line is null)
            {
                return Results.NotFound();
            }

            var invoice = await db.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == request.InvoiceId);

            if (invoice is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["invoiceId"] = ["Invoice does not exist."]
                });
            }

            var gigValidation = await ValidateInvoiceLineGigAsync(invoice, request.GigId, db);
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

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var line = await db.InvoiceLines.FirstOrDefaultAsync(value => value.Id == id);
            if (line is null)
            {
                return Results.NotFound();
            }

            db.InvoiceLines.Remove(line);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }

    private static DateTimeOffset? ResolveInvoicedAt(
        Guid? invoiceId,
        Guid? previousInvoiceId,
        DateTimeOffset? currentInvoicedAt,
        DateTimeOffset? requestedInvoicedAt)
    {
        if (!invoiceId.HasValue)
        {
            return null;
        }

        if (requestedInvoicedAt.HasValue)
        {
            return requestedInvoicedAt.Value;
        }

        if (previousInvoiceId == invoiceId)
        {
            return currentInvoicedAt;
        }

        return DateTimeOffset.UtcNow;
    }

    private static async Task<IResult?> ValidateInvoiceLineGigAsync(Invoice invoice, Guid? gigId, AppDbContext db)
    {
        if (!gigId.HasValue)
        {
            return null;
        }

        var gig = await db.Gigs
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.Id == gigId.Value);

        if (gig is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["gigId"] = ["Gig does not exist."]
            });
        }

        if (gig.ClientId != invoice.ClientId)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["gigId"] = ["Gig client must match the invoice client."]
            });
        }

        return null;
    }

    private static IQueryable<Client> WhereVisibleTo(this IQueryable<Client> query, Guid? userId)
    {
        return query.Where(client => client.CreatedByUserId == null || client.CreatedByUserId == userId);
    }

    private static void StampCreate(Client client, Guid? userId)
    {
        client.CreatedByUserId = userId;
        client.UpdatedByUserId = userId;
    }

    private static void StampUpdate(Client client, Guid? userId)
    {
        client.UpdatedByUserId = userId;
    }

    private static void StampCreate(Gig gig, Guid? userId)
    {
        gig.CreatedByUserId = userId;
        gig.UpdatedByUserId = userId;
    }

    private static void StampUpdate(Gig gig, Guid? userId)
    {
        gig.UpdatedByUserId = userId;
    }

    private static void StampCreate(Invoice invoice, Guid? userId)
    {
        invoice.CreatedByUserId = userId;
        invoice.UpdatedByUserId = userId;
    }

    private static void StampUpdate(Invoice invoice, Guid? userId)
    {
        invoice.UpdatedByUserId = userId;
    }
}
