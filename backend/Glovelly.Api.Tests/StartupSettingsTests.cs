using Glovelly.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class StartupSettingsTests
{
    [Fact]
    public void From_PostgresConnectionConfigured_UsesPostgresAndDoesNotSeedDevelopmentData()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Glovelly"] = "Host=localhost;Database=glovelly;Username=postgres;Password=postgres"
            })
            .Build();

        var settings = StartupSettings.From(configuration, new TestHostEnvironment("Development"));

        Assert.True(settings.UsePostgres);
        Assert.False(settings.ShouldSeedDevelopmentData);
    }

    [Fact]
    public void From_NoConnectionStringInDevelopment_UsesInMemoryDevelopmentSeedData()
    {
        var configuration = new ConfigurationBuilder().Build();

        var settings = StartupSettings.From(configuration, new TestHostEnvironment("Development"));

        Assert.False(settings.UsePostgres);
        Assert.True(settings.ShouldSeedDevelopmentData);
    }

    [Fact]
    public void From_ProductionWithoutPublicBaseUrl_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            StartupSettings.From(configuration, new TestHostEnvironment("Production")));

        Assert.Equal("App:PublicBaseUrl must be configured outside Development and Testing.", exception.Message);
    }

    [Fact]
    public void From_ProductionWithPublicBaseUrl_UsesCanonicalOrigin()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:PublicBaseUrl"] = "https://menu.glovelly.net/"
            })
            .Build();

        var settings = StartupSettings.From(configuration, new TestHostEnvironment("Production"));

        Assert.Equal("https://menu.glovelly.net/", settings.PublicBaseUri.ToString());
        Assert.Equal("https://menu.glovelly.net/auth/login", settings.BuildPublicUrl("/auth/login"));
    }

    [Fact]
    public void From_LegacyApplicationHostMatchesPublicOrigin_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:PublicBaseUrl"] = "https://menu.glovelly.net",
                ["App:LegacyApplicationHost"] = "menu.glovelly.net",
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            StartupSettings.From(configuration, new TestHostEnvironment("Production")));

        Assert.Equal("App:LegacyApplicationHost must differ from App:PublicBaseUrl.", exception.Message);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Glovelly.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
