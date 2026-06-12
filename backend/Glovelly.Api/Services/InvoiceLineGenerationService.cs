using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Glovelly.Api.Services;

public sealed class InvoiceLineGenerationService(
    AppDbContext dbContext,
    IOptions<InvoiceRateSettings> rateSettings) : IInvoiceLineGenerationService
{
    public async Task<List<InvoiceLine>> BuildGeneratedInvoiceLinesForGigAsync(
        Gig gig,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<InvoiceLine>();
        var nextSortOrder = await GetNextSortOrderAsync(gig.InvoiceId!.Value, cancellationToken);
        var (mileageRate, passengerMileageRate) =
            await ResolveMileageRatesAsync(gig.ClientId, userId, cancellationToken);

        if (gig.Fee != 0)
        {
            lines.Add(CreateGeneratedLine(
                gig,
                userId,
                nextSortOrder++,
                InvoiceLineType.PerformanceFee,
                $"Performance fee for {gig.Title} ({gig.Date:yyyy-MM-dd})",
                1m,
                gig.Fee));
        }

        if (gig.WasDriving &&
            gig.TravelMiles > 0 &&
            mileageRate.HasValue &&
            mileageRate.Value != 0)
        {
            lines.Add(CreateGeneratedLine(
                gig,
                userId,
                nextSortOrder++,
                InvoiceLineType.Mileage,
                $"Mileage for {gig.Title}",
                gig.TravelMiles,
                mileageRate.Value,
                $"{gig.TravelMiles:0.##} miles at {mileageRate.Value:0.##} per mile."));
        }

        var passengerCount = gig.PassengerCount.GetValueOrDefault();

        if (gig.WasDriving &&
            gig.TravelMiles > 0 &&
            passengerCount > 0 &&
            passengerMileageRate.HasValue &&
            passengerMileageRate.Value != 0)
        {
            var passengerMiles = gig.TravelMiles * passengerCount;
            var passengerLabel = passengerCount == 1 ? "passenger" : "passengers";

            lines.Add(CreateGeneratedLine(
                gig,
                userId,
                nextSortOrder++,
                InvoiceLineType.PassengerMileage,
                $"Passenger mileage for {gig.Title}",
                passengerMiles,
                passengerMileageRate.Value,
                $"{passengerCount} {passengerLabel} x {gig.TravelMiles:0.##} miles."));
        }

        foreach (var expense in gig.Expenses
                     .Where(expense => expense.Amount != 0)
                     .Where(expense => expense.IsChargeableByDefault())
                     .OrderBy(expense => expense.SortOrder)
                     .ThenBy(expense => expense.Description))
        {
            lines.Add(CreateGeneratedLine(
                gig,
                userId,
                nextSortOrder++,
                InvoiceLineType.MiscExpense,
                expense.Description,
                1m,
                expense.Amount));
        }

        return lines;
    }

    public async Task<bool> RemoveSystemGeneratedInvoiceLinesForGigAsync(
        Guid gigId,
        CancellationToken cancellationToken = default)
    {
        var generatedLines = await dbContext.InvoiceLines
            .Where(line => line.GigId == gigId && line.IsSystemGenerated)
            .ToListAsync(cancellationToken);

        if (generatedLines.Count == 0)
        {
            return false;
        }

        dbContext.InvoiceLines.RemoveRange(generatedLines);
        return true;
    }

    public async Task<InvoiceLine> CreateManualAdjustmentAsync(
        Invoice invoice,
        decimal amount,
        string reason,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var adjustmentLine = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            SortOrder = await GetNextSortOrderAsync(invoice.Id, cancellationToken),
            Type = InvoiceLineType.ManualAdjustment,
            Description = $"Manual adjustment: {reason}",
            Quantity = 1m,
            UnitPrice = amount,
            CalculationNotes = BuildAdjustmentAuditNote(userId),
            IsSystemGenerated = false,
        };
        StampCreate(adjustmentLine, userId);

        dbContext.InvoiceLines.Add(adjustmentLine);

        return adjustmentLine;
    }

    private static InvoiceLine CreateGeneratedLine(
        Gig gig,
        Guid? userId,
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
            CreatedByUserId = userId,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
    }

    private async Task<int> GetNextSortOrderAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var persistedMax = await dbContext.InvoiceLines
            .Where(line => line.InvoiceId == invoiceId)
            .Select(line => (int?)line.SortOrder)
            .MaxAsync(cancellationToken);
        var trackedMax = dbContext.ChangeTracker
            .Entries<InvoiceLine>()
            .Where(entry => entry.State is not EntityState.Deleted and not EntityState.Detached)
            .Where(entry => entry.Entity.InvoiceId == invoiceId)
            .Select(entry => (int?)entry.Entity.SortOrder)
            .Max();

        return Math.Max(persistedMax ?? 0, trackedMax ?? 0) + 1;
    }

    private async Task<(decimal? MileageRate, decimal? PassengerMileageRate)> ResolveMileageRatesAsync(
        Guid clientId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var clientRates = await dbContext.Clients
            .AsNoTracking()
            .Where(client => client.Id == clientId)
            .Select(client => new
            {
                client.MileageRate,
                client.PassengerMileageRate,
            })
            .FirstAsync(cancellationToken);

        if (!userId.HasValue)
        {
            return (
                clientRates.MileageRate ?? rateSettings.Value.DefaultMileageRate,
                clientRates.PassengerMileageRate ?? rateSettings.Value.DefaultPassengerMileageRate);
        }

        var userRates = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId.Value)
            .Select(user => new
            {
                user.MileageRate,
                user.PassengerMileageRate,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return (
            clientRates.MileageRate ?? userRates?.MileageRate ?? rateSettings.Value.DefaultMileageRate,
            clientRates.PassengerMileageRate ?? userRates?.PassengerMileageRate ?? rateSettings.Value.DefaultPassengerMileageRate);
    }

    private static void StampCreate(InvoiceLine line, Guid? userId)
    {
        line.CreatedByUserId = userId;
        line.CreatedUtc = DateTimeOffset.UtcNow;
    }

    private static string BuildAdjustmentAuditNote(Guid? userId)
    {
        var actor = userId?.ToString() ?? "unknown-user";
        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        return $"Created by {actor} at {timestamp}";
    }
}
