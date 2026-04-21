using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class SellerProfileEndpoints
{
    public static RouteGroupBuilder MapSellerProfileEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", [Authorize(Policy = GlovellyPolicies.GlovellyUser)] async (
            AppDbContext dbContext,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var profile = await dbContext.SellerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.UserId == userId.Value);

            return Results.Ok(ToResponse(profile, userId.Value));
        });

        group.MapPut("/", [Authorize(Policy = GlovellyPolicies.GlovellyUser)] async (
            SellerProfileRequest request,
            AppDbContext dbContext,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var validationErrors = ValidateRequest(request);
            if (validationErrors is not null)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var userId = currentUserAccessor.TryGetUserId(user);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var profile = await dbContext.SellerProfiles
                .FirstOrDefaultAsync(value => value.UserId == userId.Value);

            var now = DateTimeOffset.UtcNow;
            if (profile is null)
            {
                profile = new SellerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                };
                EndpointSupport.StampCreate(profile, userId);
                dbContext.SellerProfiles.Add(profile);
            }

            profile.SellerName = Normalize(request.SellerName);
            profile.Address = new Address
            {
                Line1 = NormalizeRequired(request.AddressLine1),
                Line2 = Normalize(request.AddressLine2),
                City = NormalizeRequired(request.City),
                StateOrCounty = Normalize(request.Region),
                PostalCode = NormalizeRequired(request.Postcode),
                Country = NormalizeRequired(request.Country),
            };
            profile.Email = Normalize(request.Email);
            profile.Phone = Normalize(request.Phone);
            profile.AccountName = Normalize(request.AccountName);
            profile.SortCode = Normalize(request.SortCode);
            profile.AccountNumber = Normalize(request.AccountNumber);
            profile.PaymentReferenceNote = Normalize(request.PaymentReferenceNote);
            profile.UpdatedUtc = now;
            EndpointSupport.StampUpdate(profile, userId);

            await dbContext.SaveChangesAsync();

            return Results.Ok(ToResponse(profile, userId.Value));
        });

        return group;
    }

    private static object ToResponse(SellerProfile? profile, Guid userId)
    {
        var effectiveProfile = profile ?? new SellerProfile
        {
            UserId = userId,
            Address = new Address(),
        };
        var missingFields = GetMissingFields(effectiveProfile);

        return new
        {
            id = effectiveProfile.Id == Guid.Empty ? (Guid?)null : effectiveProfile.Id,
            sellerName = effectiveProfile.SellerName,
            addressLine1 = effectiveProfile.Address.Line1,
            addressLine2 = effectiveProfile.Address.Line2,
            city = effectiveProfile.Address.City,
            region = effectiveProfile.Address.StateOrCounty,
            postcode = effectiveProfile.Address.PostalCode,
            country = effectiveProfile.Address.Country,
            email = effectiveProfile.Email,
            phone = effectiveProfile.Phone,
            accountName = effectiveProfile.AccountName,
            sortCode = effectiveProfile.SortCode,
            accountNumber = effectiveProfile.AccountNumber,
            paymentReferenceNote = effectiveProfile.PaymentReferenceNote,
            isConfigured = HasAnyValue(effectiveProfile),
            isInvoiceReady = missingFields.Count == 0,
            missingFields,
        };
    }

    private static List<string> GetMissingFields(SellerProfile profile)
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.SellerName))
        {
            missingFields.Add("sellerName");
        }

        if (string.IsNullOrWhiteSpace(profile.Address.Line1))
        {
            missingFields.Add("addressLine1");
        }

        if (string.IsNullOrWhiteSpace(profile.Address.City))
        {
            missingFields.Add("city");
        }

        if (string.IsNullOrWhiteSpace(profile.Address.Country))
        {
            missingFields.Add("country");
        }

        var hasPaymentValue =
            !string.IsNullOrWhiteSpace(profile.AccountName) ||
            !string.IsNullOrWhiteSpace(profile.SortCode) ||
            !string.IsNullOrWhiteSpace(profile.AccountNumber);

        if (hasPaymentValue)
        {
            if (string.IsNullOrWhiteSpace(profile.AccountName))
            {
                missingFields.Add("accountName");
            }

            if (string.IsNullOrWhiteSpace(profile.SortCode))
            {
                missingFields.Add("sortCode");
            }

            if (string.IsNullOrWhiteSpace(profile.AccountNumber))
            {
                missingFields.Add("accountNumber");
            }
        }

        return missingFields;
    }

    private static bool HasAnyValue(SellerProfile profile)
    {
        return new[]
        {
            profile.SellerName,
            profile.Address.Line1,
            profile.Address.Line2,
            profile.Address.City,
            profile.Address.StateOrCounty,
            profile.Address.PostalCode,
            profile.Address.Country,
            profile.Email,
            profile.Phone,
            profile.AccountName,
            profile.SortCode,
            profile.AccountNumber,
            profile.PaymentReferenceNote,
        }.Any(value => !string.IsNullOrWhiteSpace(value));
    }

    private static Dictionary<string, string[]>? ValidateRequest(SellerProfileRequest request)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(request);
        var isValid = Validator.TryValidateObject(
            request,
            validationContext,
            validationResults,
            validateAllProperties: true);

        var errors = validationResults
            .GroupBy(result => ToCamelCase(result.MemberNames.FirstOrDefault() ?? string.Empty))
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping.Select(result => result.ErrorMessage ?? "Invalid value.").ToArray());

        var hasAnyPaymentValue =
            !string.IsNullOrWhiteSpace(request.AccountName) ||
            !string.IsNullOrWhiteSpace(request.SortCode) ||
            !string.IsNullOrWhiteSpace(request.AccountNumber);

        if (hasAnyPaymentValue)
        {
            AddRequiredError(
                errors,
                nameof(request.AccountName),
                request.AccountName,
                "Account name is required when payment details are provided.");
            AddRequiredError(
                errors,
                nameof(request.SortCode),
                request.SortCode,
                "Sort code is required when payment details are provided.");
            AddRequiredError(
                errors,
                nameof(request.AccountNumber),
                request.AccountNumber,
                "Account number is required when payment details are provided.");
        }

        return isValid && errors.Count == 0 ? null : errors;
    }

    private static void AddRequiredError(
        Dictionary<string, string[]> errors,
        string memberName,
        string? value,
        string message)
    {
        var key = ToCamelCase(memberName);
        if (!errors.ContainsKey(key) && string.IsNullOrWhiteSpace(value))
        {
            errors[key] = [message];
        }
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string NormalizeRequired(string? value)
    {
        return Normalize(value) ?? string.Empty;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    internal sealed record SellerProfileRequest(
        [StringLength(200)] string? SellerName,
        [StringLength(200)] string? AddressLine1,
        [StringLength(200)] string? AddressLine2,
        [StringLength(100)] string? City,
        [StringLength(100)] string? Region,
        [StringLength(20)] string? Postcode,
        [StringLength(100)] string? Country,
        [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
        [StringLength(320)]
        string? Email,
        [Phone(ErrorMessage = "Phone must be a valid phone number.")]
        [StringLength(50)]
        string? Phone,
        [StringLength(200)] string? AccountName,
        [StringLength(20)] string? SortCode,
        [StringLength(20)] string? AccountNumber,
        [StringLength(500)] string? PaymentReferenceNote);
}
