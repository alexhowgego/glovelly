using Google.Cloud.Storage.V1;
using Glovelly.Api.Configuration;
using Microsoft.Extensions.Options;
using Resend;

namespace Glovelly.Api.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGlovellyApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AccessRequestWorkflowService>();
        services.AddScoped<AccessRequestRetentionService>();
        services.AddScoped<IInvoiceWorkflowService, InvoiceWorkflowService>();
        services.AddScoped<IInvoiceDeliveryService, InvoiceDeliveryService>();
        services.AddScoped<IInvoiceDeliveryChannel, InvoiceEmailDeliveryChannel>();
        services.AddScoped<IInvoiceDeliveryChannel, InvoiceGoogleDriveDeliveryChannel>();
        services.AddOptions<ExpenseAttachmentSettings>()
            .BindConfiguration(ExpenseAttachmentSettings.SectionName);
        services.AddSingleton<IExpenseAttachmentStore>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<ExpenseAttachmentSettings>>().Value;
            if (string.IsNullOrWhiteSpace(settings.BucketName))
            {
                var startupSettings = provider.GetRequiredService<StartupSettings>();
                if (!startupSettings.IsDevelopment)
                {
                    throw new InvalidOperationException(
                        "Expense attachment storage requires ExpenseAttachments:BucketName outside local development.");
                }

                return new InMemoryExpenseAttachmentStore();
            }

            return ActivatorUtilities.CreateInstance<GcsExpenseAttachmentStore>(provider, StorageClient.Create());
        });
        services.AddOptions<ResendClientOptions>()
            .Configure<IOptions<EmailSettings>>((resendOptions, emailOptions) =>
            {
                resendOptions.ApiToken = emailOptions.Value.Resend.ApiKey ?? string.Empty;
            });
        services.AddHttpClient<ResendClient>();
        services.AddHttpClient<IGoogleDriveApiClient, GoogleDriveApiClient>();
        services.AddScoped<IResend, ResendClient>();
        services.AddScoped<IEmailSender>(provider =>
        {
            var emailSettings = provider.GetRequiredService<IOptions<EmailSettings>>().Value;

            return NormalizeMode(emailSettings.Mode) switch
            {
                EmailModes.Disabled => ActivatorUtilities.CreateInstance<NullEmailSender>(provider),
                EmailModes.Resend => ActivatorUtilities.CreateInstance<ResendApiEmailSender>(provider, emailSettings),
                _ => ActivatorUtilities.CreateInstance<LoggingEmailSender>(provider),
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

        if (string.Equals(mode, EmailModes.Resend, StringComparison.OrdinalIgnoreCase))
        {
            return EmailModes.Resend;
        }

        return EmailModes.Log;
    }
}
