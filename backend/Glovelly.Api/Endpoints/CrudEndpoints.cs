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
            var pricingValidation = ValidateClientPricing(client);
            if (pricingValidation is not null)
            {
                return pricingValidation;
            }

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

            var pricingValidation = ValidateClientPricing(request);
            if (pricingValidation is not null)
            {
                return pricingValidation;
            }

            client.Name = request.Name.Trim();
            client.Email = request.Email.Trim();
            client.BillingAddress = request.BillingAddress ?? new Address();
            client.MileageRate = request.MileageRate;
            client.PassengerMileageRate = request.PassengerMileageRate;
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
                .Include(gig => gig.Expenses)
                .OrderBy(gig => gig.Date)
                .ThenBy(gig => gig.Title)
                .ToListAsync();

            return Results.Ok(gigs);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var gig = await db.Gigs
                .AsNoTracking()
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(gig => gig.Id == id);

            return gig is null ? Results.NotFound() : Results.Ok(gig);
        });

        group.MapPost("/", async (Gig gig, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gigValidation = ValidateGigRequest(gig);
            if (gigValidation is not null)
            {
                return gigValidation;
            }

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
            gig.Expenses = NormalizeGigExpenses(gig.Expenses);
            gig.InvoicedAt = ResolveInvoicedAt(gig.InvoiceId, null, null, gig.InvoicedAt);
            StampCreate(gig, userId);

            db.Gigs.Add(gig);
            await SyncGeneratedInvoiceLinesForGigAsync(gig, userId, db);
            await db.SaveChangesAsync();

            return Results.Created($"/gigs/{gig.Id}", gig);
        });

        group.MapPut("/{id:guid}", async (Guid id, Gig request, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (gig is null)
            {
                return Results.NotFound();
            }

            var gigValidation = ValidateGigRequest(request);
            if (gigValidation is not null)
            {
                return gigValidation;
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

            var normalizedExpenses = NormalizeGigExpenses(request.Expenses);
            _ = await RemoveSystemGeneratedInvoiceLinesForGigAsync(gig.Id, db);

            if (gig.Expenses.Count > 0)
            {
                db.GigExpenses.RemoveRange(gig.Expenses.ToList());
            }

            await db.SaveChangesAsync();

            gig = await db.Gigs
                .Include(value => value.Expenses)
                .FirstAsync(value => value.Id == id);

            var previousInvoiceId = gig.InvoiceId;

            gig.ClientId = request.ClientId;
            gig.InvoiceId = request.InvoiceId;
            gig.Title = request.Title.Trim();
            gig.Date = request.Date;
            gig.Venue = request.Venue.Trim();
            gig.Fee = request.Fee;
            gig.TravelMiles = request.TravelMiles;
            gig.PassengerCount = request.PassengerCount;
            gig.Notes = request.Notes?.Trim();
            gig.WasDriving = request.WasDriving;
            gig.Status = request.Status;
            gig.InvoicedAt = ResolveInvoicedAt(request.InvoiceId, previousInvoiceId, gig.InvoicedAt, request.InvoicedAt);
            gig.Expenses.Clear();
            foreach (var expense in normalizedExpenses)
            {
                gig.Expenses.Add(expense);
            }
            StampUpdate(gig, userId);

            await SyncGeneratedInvoiceLinesForGigAsync(gig, userId, db);
            await db.SaveChangesAsync();

            return Results.Ok(gig);
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var gig = await db.Gigs
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(gig => gig.Id == id);
            if (gig is null)
            {
                return Results.NotFound();
            }

            _ = await RemoveSystemGeneratedInvoiceLinesForGigAsync(gig.Id, db);
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

    private static IResult? ValidateClientPricing(Client client)
    {
        if (client.MileageRate.HasValue && client.MileageRate.Value < 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["mileageRate"] = ["Mileage rate cannot be negative."]
            });
        }

        if (client.PassengerMileageRate.HasValue && client.PassengerMileageRate.Value < 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["passengerMileageRate"] = ["Passenger mileage rate cannot be negative."]
            });
        }

        return null;
    }

    private static IResult? ValidateGigRequest(Gig gig)
    {
        var errors = new Dictionary<string, string[]>();

        if (gig.ClientId == Guid.Empty)
        {
            errors["clientId"] = ["Client is required."];
        }

        if (string.IsNullOrWhiteSpace(gig.Title))
        {
            errors["title"] = ["Title is required."];
        }

        if (gig.Date == default)
        {
            errors["date"] = ["Date is required."];
        }

        if (string.IsNullOrWhiteSpace(gig.Venue))
        {
            errors["venue"] = ["Location or venue is required."];
        }

        if (gig.Fee < 0)
        {
            errors["fee"] = ["Fee cannot be negative."];
        }

        if (gig.TravelMiles < 0)
        {
            errors["travelMiles"] = ["Travel miles cannot be negative."];
        }

        if (!Enum.IsDefined(gig.Status))
        {
            errors["status"] = ["Status is invalid."];
        }

        if (gig.PassengerCount.HasValue && gig.PassengerCount.Value < 0)
        {
            errors["passengerCount"] = ["Passenger count cannot be negative."];
        }

        if (gig.Expenses is not null)
        {
            var invalidDescription = gig.Expenses.Any(expense => string.IsNullOrWhiteSpace(expense.Description));
            if (invalidDescription)
            {
                errors["expenses"] = ["Each expense must include a description."];
            }

            var invalidAmount = gig.Expenses.Any(expense => expense.Amount < 0);
            if (invalidAmount)
            {
                errors["expenses"] = ["Expense amounts cannot be negative."];
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return null;
    }

    private static List<GigExpense> NormalizeGigExpenses(ICollection<GigExpense>? expenses)
    {
        if (expenses is null || expenses.Count == 0)
        {
            return [];
        }

        return expenses
            .Select((expense, index) => new GigExpense
            {
                Id = expense.Id == Guid.Empty ? Guid.NewGuid() : expense.Id,
                SortOrder = expense.SortOrder == 0 ? index + 1 : expense.SortOrder,
                Description = expense.Description.Trim(),
                Amount = expense.Amount,
            })
            .OrderBy(expense => expense.SortOrder)
            .ThenBy(expense => expense.Description)
            .ToList();
    }

    private static async Task SyncGeneratedInvoiceLinesForGigAsync(Gig gig, Guid? userId, AppDbContext db)
    {
        _ = await RemoveSystemGeneratedInvoiceLinesForGigAsync(gig.Id, db);

        if (!gig.InvoiceId.HasValue)
        {
            return;
        }

        var lines = await BuildGeneratedInvoiceLinesForGigAsync(gig, userId, db);
        if (lines.Count == 0)
        {
            return;
        }

        db.InvoiceLines.AddRange(lines);
    }

    private static async Task<bool> RemoveSystemGeneratedInvoiceLinesForGigAsync(Guid gigId, AppDbContext db)
    {
        var generatedLines = await db.InvoiceLines
            .Where(line => line.GigId == gigId && line.IsSystemGenerated)
            .ToListAsync();

        if (generatedLines.Count == 0)
        {
            return false;
        }

        db.InvoiceLines.RemoveRange(generatedLines);
        return true;
    }

    private static async Task<List<InvoiceLine>> BuildGeneratedInvoiceLinesForGigAsync(Gig gig, Guid? userId, AppDbContext db)
    {
        var lines = new List<InvoiceLine>();
        var nextSortOrder = await GetNextSortOrderAsync(gig.InvoiceId!.Value, db);
        var (mileageRate, passengerMileageRate) = await ResolveMileageRatesAsync(gig.ClientId, userId, db);

        if (gig.Fee != 0)
        {
            lines.Add(CreateGeneratedLine(
                gig,
                nextSortOrder++,
                InvoiceLineType.PerformanceFee,
                $"Performance fee for {gig.Title} ({gig.Date:yyyy-MM-dd})",
                1m,
                gig.Fee));
        }

        if (gig.TravelMiles > 0 && mileageRate.HasValue && mileageRate.Value != 0)
        {
            lines.Add(CreateGeneratedLine(
                gig,
                nextSortOrder++,
                InvoiceLineType.Mileage,
                $"Mileage for {gig.Title}",
                gig.TravelMiles,
                mileageRate.Value,
                $"{gig.TravelMiles:0.##} miles at {mileageRate.Value:0.##} per mile."));
        }

        var passengerCount = gig.PassengerCount.GetValueOrDefault();

        if (gig.TravelMiles > 0 &&
            passengerCount > 0 &&
            passengerMileageRate.HasValue &&
            passengerMileageRate.Value != 0)
        {
            var passengerMiles = gig.TravelMiles * passengerCount;
            var passengerLabel = passengerCount == 1 ? "passenger" : "passengers";

            lines.Add(CreateGeneratedLine(
                gig,
                nextSortOrder++,
                InvoiceLineType.PassengerMileage,
                $"Passenger mileage for {gig.Title}",
                passengerMiles,
                passengerMileageRate.Value,
                $"{passengerCount} {passengerLabel} x {gig.TravelMiles:0.##} miles."));
        }

        foreach (var expense in gig.Expenses
                     .Where(expense => expense.Amount != 0)
                     .OrderBy(expense => expense.SortOrder)
                     .ThenBy(expense => expense.Description))
        {
            lines.Add(CreateGeneratedLine(
                gig,
                nextSortOrder++,
                InvoiceLineType.MiscExpense,
                expense.Description,
                1m,
                expense.Amount));
        }

        return lines;
    }

    private static InvoiceLine CreateGeneratedLine(
        Gig gig,
        int sortOrder,
        InvoiceLineType type,
        string description,
        decimal quantity,
        decimal unitPrice,
        string? calculationNotes = null)
    {
        return new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = gig.InvoiceId!.Value,
            SortOrder = sortOrder,
            Type = type,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            GigId = gig.Id,
            CalculationNotes = calculationNotes,
            IsSystemGenerated = true,
        };
    }

    private static async Task<int> GetNextSortOrderAsync(Guid invoiceId, AppDbContext db)
    {
        var currentMax = await db.InvoiceLines
            .Where(line => line.InvoiceId == invoiceId)
            .Select(line => (int?)line.SortOrder)
            .MaxAsync();

        return (currentMax ?? 0) + 1;
    }

    private static async Task<(decimal? MileageRate, decimal? PassengerMileageRate)> ResolveMileageRatesAsync(
        Guid clientId,
        Guid? userId,
        AppDbContext db)
    {
        var clientRates = await db.Clients
            .AsNoTracking()
            .Where(client => client.Id == clientId)
            .Select(client => new
            {
                client.MileageRate,
                client.PassengerMileageRate,
            })
            .FirstAsync();

        if (clientRates.MileageRate.HasValue || clientRates.PassengerMileageRate.HasValue || !userId.HasValue)
        {
            return (clientRates.MileageRate, clientRates.PassengerMileageRate);
        }

        var userRates = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId.Value)
            .Select(user => new
            {
                user.MileageRate,
                user.PassengerMileageRate,
            })
            .FirstOrDefaultAsync();

        return (userRates?.MileageRate, userRates?.PassengerMileageRate);
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
