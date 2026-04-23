using Glovelly.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class EmailInfrastructureTests
{
    [Fact]
    public async Task LoggingSender_IsDefault_AndDoesNotRequireConfiguredFromAddress()
    {
        using var services = BuildServices();

        var sender = services.GetRequiredService<IEmailSender>();

        await sender.SendAsync(new EmailMessage(
            To: [new EmailAddress("user@example.com")],
            Subject: "Test subject",
            PlainTextBody: "Hello from Glovelly."));

        Assert.IsType<LoggingEmailSender>(sender);
    }

    [Fact]
    public void DisabledMode_ResolvesNullSender()
    {
        using var services = BuildServices(settings =>
        {
            settings.Mode = EmailModes.Disabled;
        });

        var sender = services.GetRequiredService<IEmailSender>();

        Assert.IsType<NullEmailSender>(sender);
    }

    [Fact]
    public void ResendMode_ResolvesResendSender()
    {
        using var services = BuildServices(settings =>
        {
            settings.Mode = "resend";
            settings.Resend.ApiKey = "re_test_key";
        });

        var sender = services.GetRequiredService<IEmailSender>();

        Assert.IsType<ResendApiEmailSender>(sender);
    }

    [Fact]
    public async Task ResendSender_RejectsMissingApiConfiguration()
    {
        using var services = BuildServices(settings =>
        {
            settings.Mode = EmailModes.Resend;
        });

        var sender = services.GetRequiredService<IEmailSender>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(
            new EmailMessage(
                To: [new EmailAddress("user@example.com")],
                Subject: "Test subject",
                PlainTextBody: "Hello from Glovelly.",
                From: new EmailAddress("access@glovelly.test", "Glovelly Access"))));

        Assert.Contains("Email:Resend:ApiKey", error.Message);
    }

    private static ServiceProvider BuildServices(Action<EmailSettings>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<EmailSettings>()
            .Configure(settings => configure?.Invoke(settings));
        services.AddGlovellyApplicationServices();

        return services.BuildServiceProvider();
    }
}
