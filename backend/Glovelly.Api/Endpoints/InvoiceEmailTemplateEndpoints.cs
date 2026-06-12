using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class InvoiceEmailTemplateEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEmailTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/invoice-email-template")
            .RequireAuthorization(new AuthorizeAttribute { Policy = GlovellyPolicies.GlovellyUser });

        group.MapPost("/preview", async (
            InvoiceEmailTemplatePreviewRequest request,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext,
            IInvoiceProfileDefaultsService invoiceProfileDefaultsService,
            CancellationToken cancellationToken) =>
        {
            if (InvoiceEmailTemplateRenderer.TryValidateBodyTemplate(
                    request.BodyTemplate,
                    out var bodyTemplateErrors,
                    "bodyTemplate"))
            {
                return Results.ValidationProblem(bodyTemplateErrors);
            }

            var userId = currentUserAccessor.TryGetUserId(user);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var localUser = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == userId.Value && value.IsActive, cancellationToken);
            if (localUser is null)
            {
                return Results.Unauthorized();
            }

            var sampleInvoice = BuildSampleInvoice();
            var sampleClient = new Client
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Fox & Finch Events",
                Email = "bookings@foxandfinch.co.uk",
            };
            var sellerProfile = await invoiceProfileDefaultsService.ResolveSellerProfileAsync(userId, cancellationToken);
            var businessName = sellerProfile?.SellerName ?? localUser.DisplayName;
            var subject = InvoiceEmailSubjectBuilder.Build(
                sampleInvoice,
                sampleClient,
                request.SubjectTemplate ?? localUser.InvoiceEmailSubjectPattern,
                sampleInvoice.InvoiceDate);
            var renderedEmail = InvoiceEmailTemplateRenderer.Render(
                sampleInvoice,
                sampleClient,
                request.BodyTemplate ?? localUser.InvoiceEmailBodyTemplate,
                businessName,
                request.AdditionalMessage,
                request.IncludeReceipts);

            return Results.Ok(new InvoiceEmailTemplatePreviewResponse(
                subject,
                renderedEmail.PlainTextBody,
                renderedEmail.HtmlBody,
                InvoiceEmailTemplateRenderer.GetSupportedTokenPlaceholders()));
        });

        return app;
    }

    private static Invoice BuildSampleInvoice()
    {
        var invoice = new Invoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            InvoiceNumber = "GLV-0001",
            InvoiceDate = new DateOnly(2026, 6, 12),
            DueDate = new DateOnly(2026, 6, 26),
            Status = InvoiceStatus.Issued,
        };
        invoice.Lines.Add(new InvoiceLine
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            InvoiceId = invoice.Id,
            SortOrder = 1,
            Type = InvoiceLineType.PerformanceFee,
            Description = "Live music performance",
            Quantity = 1,
            UnitPrice = 450m,
        });

        return invoice;
    }

    private sealed record InvoiceEmailTemplatePreviewRequest(
        string? SubjectTemplate,
        string? BodyTemplate,
        string? AdditionalMessage,
        bool IncludeReceipts = false);

    private sealed record InvoiceEmailTemplatePreviewResponse(
        string Subject,
        string PlainTextBody,
        string HtmlBody,
        IReadOnlyList<string> SupportedTokens);
}
