using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class GigReceiptAnalysisEndpoints
{
    public static RouteGroupBuilder MapGigReceiptAnalysisEndpoints(this RouteGroupBuilder group)
    {
        const string route = "/{gigId:guid}/expenses/{expenseId:guid}/attachments/{attachmentId:guid}/analysis";

        group.MapGet(route, async (Guid gigId, Guid expenseId, Guid attachmentId, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var attachment = await GigEndpointSupport.FindVisibleAttachmentAsync(db, currentUserAccessor.TryGetUserId(user), gigId, expenseId, attachmentId, asNoTracking: true);
            if (attachment is null) return Results.NotFound();

            var latest = await db.ReceiptAnalyses.AsNoTracking()
                .Where(analysis => analysis.ExpenseAttachmentId == attachmentId)
                .OrderByDescending(analysis => analysis.RequestedAt)
                .FirstOrDefaultAsync();
            return latest is null ? Results.NoContent() : Results.Ok(ReceiptAnalysisResult.FromAnalysis(latest));
        });

        group.MapPost(route, async (Guid gigId, Guid expenseId, Guid attachmentId, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor, IReceiptAnalysisService receiptAnalysisService, CancellationToken cancellationToken) =>
        {
            var attachment = await GigEndpointSupport.FindVisibleAttachmentAsync(db, currentUserAccessor.TryGetUserId(user), gigId, expenseId, attachmentId, asNoTracking: false);
            if (attachment is null) return Results.NotFound();
            var result = await receiptAnalysisService.AnalyzeAsync(attachment, cancellationToken, currentUserAccessor.TryGetUserId(user));
            return result.FailureCode == "rate_limited" ? Results.Json(result, statusCode: StatusCodes.Status429TooManyRequests) : Results.Ok(result);
        });

        return group;
    }
}
