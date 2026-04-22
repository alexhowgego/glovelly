using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class EndpointSupport
{
    public static IQueryable<Client> WhereVisibleTo(this IQueryable<Client> query, Guid? userId)
    {
        return query.Where(client => client.CreatedByUserId == null || client.CreatedByUserId == userId);
    }

    public static IQueryable<Gig> WhereVisibleTo(this IQueryable<Gig> query, Guid? userId)
    {
        return query.Where(gig => gig.CreatedByUserId == null || gig.CreatedByUserId == userId);
    }

    public static IQueryable<Invoice> WhereVisibleTo(this IQueryable<Invoice> query, Guid? userId)
    {
        return query.Where(invoice => invoice.CreatedByUserId == null || invoice.CreatedByUserId == userId);
    }

    public static IQueryable<InvoiceLine> WhereVisibleTo(this IQueryable<InvoiceLine> query, Guid? userId)
    {
        return query.Where(line => line.CreatedByUserId == null || line.CreatedByUserId == userId);
    }

    public static IQueryable<SellerProfile> WhereVisibleTo(this IQueryable<SellerProfile> query, Guid? userId)
    {
        return query.Where(profile => profile.UserId == userId);
    }

    public static void StampCreate(Client client, Guid? userId)
    {
        client.CreatedByUserId = userId;
        client.UpdatedByUserId = userId;
    }

    public static void StampUpdate(Client client, Guid? userId)
    {
        client.UpdatedByUserId = userId;
    }

    public static void StampCreate(Gig gig, Guid? userId)
    {
        gig.CreatedByUserId = userId;
        gig.UpdatedByUserId = userId;
    }

    public static void StampUpdate(Gig gig, Guid? userId)
    {
        gig.UpdatedByUserId = userId;
    }

    public static void StampCreate(Invoice invoice, Guid? userId)
    {
        invoice.CreatedByUserId = userId;
        invoice.UpdatedByUserId = userId;
    }

    public static void StampUpdate(Invoice invoice, Guid? userId)
    {
        invoice.UpdatedByUserId = userId;
    }

    public static void StampCreate(InvoiceLine line, Guid? userId)
    {
        line.CreatedByUserId = userId;
        line.CreatedUtc = DateTimeOffset.UtcNow;
    }

    public static DateTimeOffset? ResolveInvoicedAt(
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

    public static IResult? ValidateClientPricing(Client client)
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

        if (TryValidateInvoiceFilenamePattern(client.InvoiceFilenamePattern, out var patternErrors))
        {
            return Results.ValidationProblem(patternErrors);
        }

        return null;
    }

    public static bool TryValidateInvoiceFilenamePattern(
        string? pattern,
        out Dictionary<string, string[]> errors,
        string fieldName = "invoiceFilenamePattern")
    {
        errors = new Dictionary<string, string[]>();

        if (pattern is not null && string.IsNullOrWhiteSpace(pattern))
        {
            errors[fieldName] = ["Invoice filename pattern cannot be empty or whitespace."];
            return true;
        }

        return false;
    }

    public static void StampCreate(SellerProfile profile, Guid? userId)
    {
        profile.CreatedByUserId = userId;
        profile.UpdatedByUserId = userId;
        profile.CreatedUtc = DateTimeOffset.UtcNow;
        profile.UpdatedUtc = profile.CreatedUtc;
    }

    public static void StampUpdate(SellerProfile profile, Guid? userId)
    {
        profile.UpdatedByUserId = userId;
        profile.UpdatedUtc = DateTimeOffset.UtcNow;
    }

    public static IResult? ValidateGigRequest(Gig gig)
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

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    public static List<GigExpense> NormalizeGigExpenses(ICollection<GigExpense>? expenses, bool preserveIds = true)
    {
        if (expenses is null || expenses.Count == 0)
        {
            return [];
        }

        return expenses
            .Select((expense, index) => new GigExpense
            {
                Id = preserveIds && expense.Id != Guid.Empty ? expense.Id : Guid.NewGuid(),
                SortOrder = expense.SortOrder == 0 ? index + 1 : expense.SortOrder,
                Description = expense.Description.Trim(),
                Amount = expense.Amount,
            })
            .OrderBy(expense => expense.SortOrder)
            .ThenBy(expense => expense.Description)
            .ToList();
    }

    public static IResult? ValidateInvoiceStatusTransition(InvoiceStatus currentStatus, InvoiceStatus requestedStatus)
    {
        if (currentStatus == requestedStatus)
        {
            return null;
        }

        var allowed = currentStatus switch
        {
            InvoiceStatus.Draft => requestedStatus is InvoiceStatus.Issued or InvoiceStatus.Cancelled,
            InvoiceStatus.Issued => requestedStatus is InvoiceStatus.Paid or InvoiceStatus.Cancelled or InvoiceStatus.Overdue,
            InvoiceStatus.Overdue => requestedStatus is InvoiceStatus.Paid or InvoiceStatus.Cancelled,
            InvoiceStatus.Cancelled => requestedStatus is InvoiceStatus.Draft,
            InvoiceStatus.Paid => false,
            _ => false
        };

        return allowed
            ? null
            : Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = [$"Invoice status cannot move from {currentStatus} to {requestedStatus}."]
            });
    }

    public static async Task<IResult?> ValidateInvoiceLineGigAsync(
        Invoice invoice,
        Guid? gigId,
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!gigId.HasValue)
        {
            return null;
        }

        var gig = await db.Gigs
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.Id == gigId.Value, cancellationToken);

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
}
