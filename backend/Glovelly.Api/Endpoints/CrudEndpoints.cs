using Glovelly.Api.Auth;

namespace Glovelly.Api.Endpoints;

public static class CrudEndpoints
{
    public static IEndpointRouteBuilder MapCrudEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/clients")
            .WithTags("Clients")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser)
            .MapClientEndpoints();

        app.MapGroup("/gigs")
            .WithTags("Gigs")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser)
            .MapGigEndpoints();

        app.MapGroup("/invoices")
            .WithTags("Invoices")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser)
            .MapInvoiceEndpoints();

        app.MapGroup("/invoice-lines")
            .WithTags("InvoiceLines")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser)
            .MapInvoiceLineEndpoints();

        app.MapGroup("/seller-profile")
            .WithTags("SellerProfile")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser)
            .MapSellerProfileEndpoints();

        return app;
    }
}
