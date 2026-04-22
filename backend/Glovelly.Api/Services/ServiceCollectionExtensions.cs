using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGlovellyApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInvoiceWorkflowService, InvoiceWorkflowService>();
        services.AddScoped<IEmailSender>(provider =>
        {
            var emailSettings = provider.GetRequiredService<IOptions<EmailSettings>>().Value;

            return NormalizeMode(emailSettings.Mode) switch
            {
                EmailModes.Disabled => ActivatorUtilities.CreateInstance<NullEmailSender>(provider),
                EmailModes.Smtp => ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider, emailSettings),
                _ => ActivatorUtilities.CreateInstance<LoggingEmailSender>(provider, emailSettings),
            };
        });

        return services;
    }

    private static string NormalizeMode(string? mode)
    {
        if (string.Equals(mode, EmailModes.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            return EmailModes.Disabled;
        }

        if (string.Equals(mode, EmailModes.Smtp, StringComparison.OrdinalIgnoreCase))
        {
            return EmailModes.Smtp;
        }

        return EmailModes.Log;
    }
}
