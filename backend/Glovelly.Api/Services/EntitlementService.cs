using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Glovelly.Api.Services;

public sealed record EntitlementPlan(string Code, string DisplayName);
public sealed record EntitlementFeatures(bool PaidIncomeSummary);
public sealed record LicenceSummary(int FreeUsers, int PaidUsers, int ExpiringGrants);
public sealed record InvoiceAllowance(int Used, int? Limit, int? Remaining,
    DateTimeOffset PeriodStartsAtUtc, DateTimeOffset ResetsAtUtc, string TimeZone);
public sealed record EntitlementSnapshot(
    EntitlementPlan EffectivePlan,
    EntitlementPlan? AssignedPlan,
    string AssignmentStatus,
    DateTimeOffset? EndsAtUtc,
    EntitlementFeatures Features,
    InvoiceAllowance InvoiceIssuance);

public sealed class EntitlementService(AppDbContext db, TimeProvider timeProvider)
{
    private static Expression<Func<UserSubscription, bool>> EffectiveAt(DateTimeOffset now) =>
        value => value.State == SubscriptionState.Active && value.StartsAtUtc <= now &&
            (!value.EndsAtUtc.HasValue || now < value.EndsAtUtc.Value);

    public async Task<LicenceSummary> GetLicenceSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var horizon = now.AddDays(30);
        var paid = db.UserSubscriptions.AsNoTracking().Where(EffectiveAt(now))
            .Where(value => value.PlanCode != Plan.Free);
        // Aggregate all accounts in one query, not the directory's current
        // search/page. Inactive accounts still have independent product grants.
        var summary = await (
            from user in db.Users.AsNoTracking()
            join assignment in paid on user.Id equals assignment.UserId into assignments
            from assignment in assignments.DefaultIfEmpty()
            group assignment by 1 into accounts
            select new LicenceSummary(
                accounts.Count(value => value == null),
                accounts.Count(value => value != null),
                accounts.Count(value => value != null && value.EndsAtUtc.HasValue && value.EndsAtUtc <= horizon)))
            .SingleOrDefaultAsync(cancellationToken);
        return summary ?? new LicenceSummary(0, 0, 0);
    }

    public Task<EntitlementSnapshot> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetAtAsync(userId, timeProvider.GetUtcNow(), cancellationToken);

    // Issuance supplies its captured timestamp so the allowance and ledger agree
    // even when rendering crosses a month boundary.
    public async Task<EntitlementSnapshot> GetAtAsync(
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return (await GetManyAtAsync([userId], now, cancellationToken))[userId];
    }

    public Task<Dictionary<Guid, EntitlementSnapshot>> GetManyAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        GetManyAtAsync(userIds, timeProvider.GetUtcNow(), cancellationToken);

    private async Task<Dictionary<Guid, EntitlementSnapshot>> GetManyAtAsync(
        IReadOnlyCollection<Guid> userIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var subscriptions = await db.UserSubscriptions.AsNoTracking().Where(value => ids.Contains(value.UserId))
            .ToDictionaryAsync(value => value.UserId, cancellationToken);
        var isEffective = EffectiveAt(now).Compile();
        var plans = await db.Plans.AsNoTracking().ToDictionaryAsync(value => value.Code, cancellationToken);
        var period = UkCalendarMonth.ForInstant(now);
        var usage = await db.UsageLedgerEntries.AsNoTracking()
            .Where(value => ids.Contains(value.UserId) && value.Metric == UsageLedgerEntry.InvoiceFirstIssued &&
                value.OccurredAtUtc >= period.StartsAtUtc && value.OccurredAtUtc < period.ResetsAtUtc)
            .GroupBy(value => value.UserId).Select(group => new { UserId = group.Key, Used = group.Sum(value => value.Quantity) })
            .ToDictionaryAsync(value => value.UserId, value => value.Used, cancellationToken);
        return ids.ToDictionary(userId => userId, userId =>
        {
            var subscription = subscriptions.GetValueOrDefault(userId);
            var active = subscription is not null && isEffective(subscription);
            var plan = plans[active ? subscription!.PlanCode : Plan.Free];
            var assigned = subscription is null ? null : plans[subscription.PlanCode];
            var used = usage.GetValueOrDefault(userId);
            var status = subscription is null ? "Default" :
                subscription.State == SubscriptionState.Cancelled ? "Cancelled" :
                subscription.StartsAtUtc > now ? "NotStarted" : !active ? "Expired" : "Active";
            return new EntitlementSnapshot(
                new(plan.Code, plan.DisplayName), assigned is null ? null : new(assigned.Code, assigned.DisplayName),
                status, subscription?.EndsAtUtc, new(plan.PaidIncomeSummary),
                new(used, plan.MonthlyFirstIssueLimit,
                    plan.MonthlyFirstIssueLimit.HasValue ? Math.Max(0, plan.MonthlyFirstIssueLimit.Value - used) : null,
                    period.StartsAtUtc, period.ResetsAtUtc, "Europe/London"));
        });
    }
}

public sealed record UkCalendarMonth(DateTimeOffset StartsAtUtc, DateTimeOffset ResetsAtUtc)
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static UkCalendarMonth ForInstant(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, London);
        var start = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new(
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start, London)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start.AddMonths(1), London)));
    }
}
