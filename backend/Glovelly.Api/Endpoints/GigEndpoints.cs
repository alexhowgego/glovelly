namespace Glovelly.Api.Endpoints;

public static class GigEndpoints
{
    public static RouteGroupBuilder MapGigEndpoints(this RouteGroupBuilder group)
    {
        group
            .MapGigInvoiceEndpoints()
            .MapGigCrudEndpoints()
            .MapGigReceiptEndpoints()
            .MapGigExpenseEndpoints()
            .MapGigExternalResourceEndpoints()
            .MapGigSetListImportEndpoints()
            .MapGigMileageEndpoints()
            .MapGigAttachmentEndpoints()
            .MapGigReceiptAnalysisEndpoints();

        return group;
    }
}
