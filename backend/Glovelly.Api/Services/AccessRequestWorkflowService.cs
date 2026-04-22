using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Glovelly.Api.Services;

public sealed class AccessRequestWorkflowService(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<AccessRequestProtectionSettings> settings)
{
    public const string DuplicateEmailSuppressionReason = "duplicate_email_window";
    public const string DailyIpSuppressionReason = "daily_ip_window";
    public const string DailyNotificationCapSuppressionReason = "daily_notification_cap";
    private readonly AccessRequestProtectionSettings _settings = settings.Value;

    public async Task<AccessRequestWorkflowResult> RecordAsync(
        string email,
        string? displayName,
        string? subject,
        IPAddress? remoteIpAddress,
        CancellationToken cancellationToken)
    {
        var requestedAtUtc = timeProvider.GetUtcNow();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var requestIpHash = HashIpAddress(remoteIpAddress);
        var notificationSuppressionReason = await ResolveSuppressionReasonAsync(
            normalizedEmail,
            requestIpHash,
            requestedAtUtc,
            cancellationToken);

        var request = new AccessRequest
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = Normalize(displayName),
            Subject = Normalize(subject),
            RequestedAtUtc = requestedAtUtc,
            RequestIpHash = requestIpHash,
            NotificationSuppressionReason = notificationSuppressionReason
        };

        dbContext.AccessRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccessRequestWorkflowResult(
            request,
            ShouldSendNotification: notificationSuppressionReason is null,
            notificationSuppressionReason);
    }

    public async Task MarkNotificationSentAsync(Guid accessRequestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.AccessRequests.FirstAsync(
            value => value.Id == accessRequestId,
            cancellationToken);
        request.NotificationSentAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> ResolveSuppressionReasonAsync(
        string normalizedEmail,
        string? requestIpHash,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestIpHash))
        {
            var dailyIpWindowStart = requestedAtUtc - _settings.PerIpDailyWindow;
            var recentRequestCountForIp = await dbContext.AccessRequests
                .AsNoTracking()
                .CountAsync(
                    value => value.RequestIpHash == requestIpHash
                        && value.RequestedAtUtc >= dailyIpWindowStart,
                    cancellationToken);

            if (recentRequestCountForIp >= _settings.PerIpDailyPermitLimit)
            {
                return DailyIpSuppressionReason;
            }
        }

        var duplicateWindowStart = requestedAtUtc - _settings.EmailNotificationSuppressionWindow;
        var recentNotificationExists = await dbContext.AccessRequests
            .AsNoTracking()
            .AnyAsync(
                value => value.NormalizedEmail == normalizedEmail
                    && value.NotificationSentAtUtc != null
                    && value.NotificationSentAtUtc >= duplicateWindowStart,
                cancellationToken);

        if (recentNotificationExists)
        {
            return DuplicateEmailSuppressionReason;
        }

        var globalWindowStart = requestedAtUtc - _settings.GlobalNotificationWindow;
        var recentNotificationCount = await dbContext.AccessRequests
            .AsNoTracking()
            .CountAsync(
                value => value.NotificationSentAtUtc != null
                    && value.NotificationSentAtUtc >= globalWindowStart,
                cancellationToken);

        return recentNotificationCount >= _settings.GlobalNotificationDailyCap
            ? DailyNotificationCapSuppressionReason
            : null;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? HashIpAddress(IPAddress? remoteIpAddress)
    {
        if (remoteIpAddress is null)
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(remoteIpAddress.ToString()));
        return Convert.ToHexStringLower(bytes);
    }
}

public sealed record AccessRequestWorkflowResult(
    AccessRequest AccessRequest,
    bool ShouldSendNotification,
    string? NotificationSuppressionReason);
