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

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Glovelly.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
