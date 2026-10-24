using Glovelly.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GoogleStaticMapsSettingsTests
{
    [Fact]
    public void MissingOrDisabledConfigurationIsUnavailable()
    {
        Assert.False(new GoogleStaticMapsSettings().IsAvailable);
        Assert.False(new GoogleStaticMapsSettings { ApiKey = "test-key" }.IsAvailable);
        Assert.False(new GoogleStaticMapsSettings { Enabled = true }.IsAvailable);
    }

    [Fact]
    public void SeparateStaticSectionBindsWithoutRoutesCredentials()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mileage:GoogleRoutes:ApiKey"] = "routes-only",
            ["Maps:GoogleStatic:Enabled"] = "true"
        }).Build();
        Assert.False(config.GetSection(GoogleStaticMapsSettings.SectionName).Get<GoogleStaticMapsSettings>()!.IsAvailable);
        config["Maps:GoogleStatic:ApiKey"] = "static-only";
        Assert.False(config.GetSection(GoogleStaticMapsSettings.SectionName).Get<GoogleStaticMapsSettings>()!.IsAvailable);
        config["Maps:GoogleStatic:SigningSecret"] = GoogleStaticMapsSigningTests.TestSecret;
        var settings = config.GetSection(GoogleStaticMapsSettings.SectionName).Get<GoogleStaticMapsSettings>()!;
        Assert.True(settings.IsAvailable);
        Assert.Equal("static-only", settings.ApiKey);
    }
}
