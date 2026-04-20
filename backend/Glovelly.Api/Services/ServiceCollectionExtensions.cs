using Microsoft.Extensions.DependencyInjection;

namespace Glovelly.Api.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGlovellyApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInvoiceWorkflowService, InvoiceWorkflowService>();

        return services;
    }
}
