using Google.Cloud.AIPlatform.V1;
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
        services.AddOptions<SetListChartRankingSettings>()
            .BindConfiguration(SetListChartRankingSettings.SectionName);
        services.AddScoped<AccessRequestWorkflowService>();
        services.AddScoped<AccessRequestRetentionService>();
        services.AddScoped<IExpenseStatementBuilder, ExpenseStatementBuilder>();
        services.AddScoped<IExpenseStatementPdfRenderer, ExpenseStatementPdfRenderer>();
        services.AddScoped<IInvoiceNumberService, InvoiceNumberService>();
        services.AddScoped<IInvoiceLineGenerationService, InvoiceLineGenerationService>();
        services.AddScoped<IInvoiceProfileDefaultsService, InvoiceProfileDefaultsService>();
        services.AddScoped<IInvoicePdfRenderer, InvoicePdfRenderer>();
        services.AddScoped<IInvoiceWorkflowService, InvoiceWorkflowService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();
        services.AddScoped<IInvoiceDeliveryService, InvoiceDeliveryService>();
        services.AddScoped<IGigImportDuplicateDetectionService, GigImportDuplicateDetectionService>();
        services.AddSingleton<ISetListSheetParser, SetListSheetParser>();
        services.AddScoped<ISetListChartMatcher, SetListChartMatcher>();
        services.AddScoped<DeterministicSetListChartContextualRanker>();
        services.AddScoped<VertexAiSetListChartContextualRanker>();
        services.AddScoped<ISetListChartContextualRanker>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<SetListChartRankingSettings>>().Value;
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("SetListChartRanking");
            if (settings.IsVertexAiConfigured)
            {
                logger.LogInformation(
                    "Set list chart ranking provider selected: VertexAi using model {Model} in {Location}.",
                    settings.VertexAiModel ?? "gemini-2.5-flash",
                    settings.VertexAiLocation);
                return provider.GetRequiredService<VertexAiSetListChartContextualRanker>();
            }

            logger.LogInformation("Set list chart ranking provider selected: Deterministic.");
            return provider.GetRequiredService<DeterministicSetListChartContextualRanker>();
        });
        services.AddSingleton<IForScoreLibraryParser, ForScoreLibraryParser>();
        services.AddScoped<IForScoreLibraryImportService, ForScoreLibraryImportService>();
        services.AddScoped<IGoogleConnectionService, GoogleConnectionService>();
        services.AddSingleton<ICalendarEventPayloadHasher, CalendarEventPayloadHasher>();
        services.AddScoped<IGigCalendarEventMapper, GigCalendarEventMapper>();
        services.AddScoped<IGigCalendarSyncPlanner, GigCalendarSyncPlanner>();
        services.AddScoped<IGoogleCalendarIntegrationService, GoogleCalendarIntegrationService>();
        services.AddScoped<ICalendarSyncWorkQueue, CalendarSyncWorkQueue>();
        services.AddScoped<IGoogleCalendarSyncProcessor, GoogleCalendarSyncProcessor>();
        services.AddScoped<ICalendarSyncQueueDrainer, CalendarSyncQueueDrainer>();
        services.AddScoped<IBusinessLifecycleAdvancementProcessor, BusinessLifecycleAdvancementProcessor>();
        services.AddSingleton<IBusinessLifecycleSignal, BusinessLifecycleSignal>();
        services.AddSingleton<IScheduledTaskStateStore, BlobScheduledTaskStateStore>();
        services.AddSingleton<IScheduledTaskSignal, ScheduledTaskSignal>();
        services.AddSingleton<GoogleCalendarPropagationScheduledTask>();
        services.AddSingleton<BusinessLifecycleAdvancementScheduledTask>();
        services.AddOptions<GoogleRoutesMileageSettings>()
            .BindConfiguration(GoogleRoutesMileageSettings.SectionName);
        services.AddHttpClient<GoogleRoutesMileageEstimationService>();
        services.AddScoped<DisabledMileageEstimationService>();
        services.AddScoped<IMileageEstimationService>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<GoogleRoutesMileageSettings>>().Value;
            return settings.IsConfigured
                ? provider.GetRequiredService<GoogleRoutesMileageEstimationService>()
                : provider.GetRequiredService<DisabledMileageEstimationService>();
        });
        services.AddScoped<IInvoiceDeliveryChannel, InvoiceEmailDeliveryChannel>();
        services.AddScoped<IInvoiceDeliveryChannel, InvoiceGoogleDriveDeliveryChannel>();
        services.AddOptions<InvoiceRateSettings>()
            .BindConfiguration(InvoiceRateSettings.SectionName);
        services.AddOptions<BlobStorageSettings>()
            .BindConfiguration(BlobStorageSettings.SectionName)
            .PostConfigure<IOptions<ExpenseAttachmentSettings>>((blobSettings, expenseAttachmentOptions) =>
            {
                if (string.IsNullOrWhiteSpace(blobSettings.BucketName))
                {
                    blobSettings.BucketName = expenseAttachmentOptions.Value.BucketName;
                }
            });
        services.AddOptions<ExpenseAttachmentSettings>()
            .BindConfiguration(ExpenseAttachmentSettings.SectionName);
        services.AddOptions<QuickCaptureSettings>()
            .BindConfiguration(QuickCaptureSettings.SectionName);
        services.AddSingleton<IBlobStore>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<BlobStorageSettings>>().Value;
            if (string.IsNullOrWhiteSpace(settings.BucketName))
            {
                var startupSettings = provider.GetRequiredService<StartupSettings>();
                if (!startupSettings.IsDevelopment)
                {
                    throw new InvalidOperationException(
                        "Blob storage requires BlobStorage:BucketName outside local development.");
                }

                return new InMemoryBlobStore();
            }

            return ActivatorUtilities.CreateInstance<GcsBlobStore>(provider, StorageClient.Create());
        });
        services.AddSingleton<IExpenseAttachmentStore, ExpenseAttachmentStore>();
        services.AddOptions<ResendClientOptions>()
            .Configure<IOptions<EmailSettings>>((resendOptions, emailOptions) =>
            {
                resendOptions.ApiToken = emailOptions.Value.Resend.ApiKey ?? string.Empty;
            });
        services.AddHttpClient<ResendClient>();
        services.AddHttpClient<IGoogleDriveApiClient, GoogleDriveApiClient>();
        services.AddHttpClient<IGoogleSheetsApiClient, GoogleSheetsApiClient>();
        services.AddHttpClient<IGoogleCalendarApiClient, GoogleCalendarApiClient>();
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
