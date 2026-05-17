using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class InvoiceProfileDefaultsService(AppDbContext dbContext) : IInvoiceProfileDefaultsService
{
    private const int DefaultPaymentWindowDays = 14;

    public async Task<int> ResolvePaymentWindowDaysAsync(
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (!userId.HasValue)
        {
            return DefaultPaymentWindowDays;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId.Value && user.IsActive)
            .Select(user => user.DefaultPaymentWindowDays)
            .FirstOrDefaultAsync(cancellationToken) ?? DefaultPaymentWindowDays;
    }

    public Task<SellerProfile?> ResolveSellerProfileAsync(
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (!userId.HasValue)
        {
            return Task.FromResult<SellerProfile?>(null);
        }

        return dbContext.SellerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId.Value, cancellationToken);
    }
}
