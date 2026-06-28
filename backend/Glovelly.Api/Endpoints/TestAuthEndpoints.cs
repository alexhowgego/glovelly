using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Glovelly.Api.Endpoints;

internal static class TestAuthEndpoints
{
    private const string SecretHeaderName = "X-Glovelly-Uat-Secret";

    public static IEndpointRouteBuilder MapTestAuthEndpoints(this IEndpointRouteBuilder app, StartupSettings settings)
    {
        if (!settings.IsStaging)
        {
            return app;
        }

        var group = app.MapGroup("/test-auth").AllowAnonymous();

        group.MapPost("/login", async (
            HttpContext httpContext,
            AppDbContext dbContext,
            IConfiguration configuration,
            string? returnUrl) =>
        {
            var secretCheck = ValidateSecret(httpContext, configuration);
            if (secretCheck is not null)
            {
                return secretCheck;
            }

            await UatRegressionDataSeeder.SeedAsync(dbContext);

            var user = await dbContext.Users
                .AsNoTracking()
                .FirstAsync(value => value.Id == UatRegressionDataSeeder.UserId && value.IsActive);

            var claims = new[]
            {
                new Claim(GlovellyClaimTypes.UserId, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("email", user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("role", user.Role.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
                new Claim("name", user.DisplayName ?? user.Email),
                new Claim("sub", user.GoogleSubject ?? UatRegressionDataSeeder.GoogleSubject),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    IssuedUtc = DateTimeOffset.UtcNow,
                });

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Results.Redirect(AuthFlowSupport.BuildSafeRedirectUri(httpContext, returnUrl));
            }

            return Results.Ok(new
            {
                userId = user.Id,
                email = user.Email,
                name = user.DisplayName,
                role = user.Role.ToString(),
            });
        });

        group.MapPost("/gig-import-batches", async (
            HttpContext httpContext,
            AppDbContext dbContext,
            IConfiguration configuration,
            UatGigImportBatchRequest request) =>
        {
            var secretCheck = ValidateSecret(httpContext, configuration);
            if (secretCheck is not null)
            {
                return secretCheck;
            }

            await UatRegressionDataSeeder.SeedAsync(dbContext);

            var sourceName = string.IsNullOrWhiteSpace(request.SourceName)
                ? $"Automated UAT import {DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
                : request.SourceName.Trim();
            var batch = new GigImportBatch
            {
                Id = Guid.NewGuid(),
                SourceName = sourceName,
                SourceFingerprint = request.SourceFingerprint,
                Status = GigImportBatchStatus.Draft,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedByUserId = UatRegressionDataSeeder.UserId,
                Notes = "Created by staging UAT automation setup.",
            };

            var baseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90));
            batch.Drafts.Add(new GigImportDraft
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                ProposedClientId = UatRegressionDataSeeder.ClientId,
                ProposedClientName = "UAT Regression Client",
                ProposedTitle = $"{sourceName} accepted row",
                ProposedDate = baseDate,
                ProposedVenueName = "UAT Import Hall",
                ProposedFee = 150m,
                SourceReference = "accepted-row",
                Confidence = GigImportDraftConfidence.High,
            });
            batch.Drafts.Add(new GigImportDraft
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                ProposedClientId = UatRegressionDataSeeder.ClientId,
                ProposedClientName = "UAT Regression Client",
                ProposedTitle = $"{sourceName} rejected row",
                ProposedDate = baseDate.AddDays(1),
                ProposedVenueName = "UAT Rejected Hall",
                ProposedFee = 75m,
                SourceReference = "rejected-row",
                Confidence = GigImportDraftConfidence.Low,
            });
            batch.Drafts.Add(new GigImportDraft
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                ProposedClientId = UatRegressionDataSeeder.ClientId,
                ProposedClientName = "UAT Regression Client",
                ProposedTitle = $"{sourceName} pending row",
                ProposedDate = baseDate.AddDays(2),
                ProposedVenueName = "UAT Pending Hall",
                ProposedFee = 95m,
                SourceReference = "pending-row",
                Confidence = GigImportDraftConfidence.Medium,
            });

            dbContext.GigImportBatches.Add(batch);
            await dbContext.SaveChangesAsync();

            return Results.Created($"/gig-imports/{batch.Id}", new
            {
                batchId = batch.Id,
                sourceName = batch.SourceName,
                draftCount = batch.Drafts.Count,
            });
        });

        return app;
    }

    private static IResult? ValidateSecret(HttpContext httpContext, IConfiguration configuration)
    {
        var suppliedSecret = httpContext.Request.Headers[SecretHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(suppliedSecret))
        {
            return Results.Unauthorized();
        }

        var configuredSecret = configuration["GLOVELLY_UAT_SECRET"] ?? configuration["Uat:Secret"];
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            return Results.Problem(
                detail: "Staging UAT authentication is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return SecretsMatch(suppliedSecret, configuredSecret) ? null : Results.Forbid();
    }

    private static bool SecretsMatch(string suppliedSecret, string configuredSecret)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedSecret);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredSecret);

        return suppliedBytes.Length == configuredBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }

    private sealed record UatGigImportBatchRequest(string? SourceName, string? SourceFingerprint);
}
