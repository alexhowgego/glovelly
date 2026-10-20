using System.Security.Cryptography;
using System.Text;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GoogleStaticMapsSigningTests
{
    [Theory]
    [InlineData("580x400")]
    [InlineData("640x320")]
    [InlineData("320x640")]
    public void FrameDimensionsAreSigned(string size)
    {
        using var service = CreateService();
        var result = service.Create("user", "Hall", size);
        Assert.NotNull(result);
        Assert.Equal(size, result.Size);
        Assert.Equal(size, QueryHelpers.ParseQuery(new Uri(result.Url).Query)["size"]);
        var unsigned = result.Url[..result.Url.LastIndexOf("&signature=", StringComparison.Ordinal)];
        Assert.Equal(GoogleStaticMapsService.SignPathAndQuery(new Uri(unsigned).PathAndQuery, Convert.FromBase64String(TestSecret)), QueryHelpers.ParseQuery(new Uri(result.Url).Query)["signature"]);
    }

    [Theory]
    [InlineData("641x400")]
    [InlineData("320x0")]
    [InlineData("31x400")]
    [InlineData("+320x400")]
    [InlineData("0320x400")]
    [InlineData("320x400&zoom=1")]
    public void RejectsInvalidDimensions(string size) => Assert.Null(GoogleStaticMapsService.Size(size));

    public const string TestSecret = "AAECAwQFBgcICQoLDA0ODxAREhM=";

    [Fact]
    public void SignatureMatchesIndependentUtf8Vector()
    {
        const string path = "/maps/api/staticmap?center=Caf%C3%A9%20%26%20Hall&size=200x300&key=test-only";
        Assert.Equal("dQGWZyfJnOgO7VTuu1Wn3ixPV9c", GoogleStaticMapsService.SignPathAndQuery(path, Convert.FromBase64String(TestSecret)));
    }

    [Fact]
    public void FinalUrlIsSignedAfterEncodingAndContainsOnlyLocationRenderingAndAuthentication()
    {
        using var service = CreateService();
        var result = service.Create("internal-user", "Café & Hall #1 + 東", "small");
        Assert.NotNull(result);
        var uri = new Uri(result.Url);
        Assert.Equal("https://maps.googleapis.com/maps/api/staticmap", uri.GetLeftPart(UriPartial.Path));
        var parameters = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("Café & Hall #1 + 東", parameters["center"]);
        Assert.Equal("Café & Hall #1 + 東", parameters["markers"]);
        Assert.Equal("200x300", parameters["size"]);
        Assert.Equal("test-only", parameters["key"]);
        Assert.Equal(8, parameters.Count);
        Assert.DoesNotContain("internal-user", result.Url);
        Assert.DoesNotContain(TestSecret, result.Url);
        Assert.Equal(RecordLocationSource.Fingerprint("Café & Hall #1 + 東"), result.SourceFingerprint);
        var unsigned = result.Url[..result.Url.LastIndexOf("&signature=", StringComparison.Ordinal)];
        var expected = WebEncoders.Base64UrlEncode(HMACSHA1.HashData(Convert.FromBase64String(TestSecret), Encoding.UTF8.GetBytes(new Uri(unsigned).PathAndQuery)));
        Assert.Equal(expected, parameters["signature"]);
    }

    [Fact]
    public void InvalidAndDisabledConfigurationNeverFallsBackToUnsignedUrls()
    {
        foreach (var settings in new[]
        {
            new GoogleStaticMapsSettings { Enabled = false, ApiKey = "test-only", SigningSecret = TestSecret },
            new GoogleStaticMapsSettings { Enabled = true, ApiKey = "test-only" },
            new GoogleStaticMapsSettings { Enabled = true, ApiKey = "test-only", SigningSecret = "invalid!" },
            new GoogleStaticMapsSettings { Enabled = true, ApiKey = "", SigningSecret = TestSecret },
            new GoogleStaticMapsSettings { Enabled = true, ApiKey = "bad key", SigningSecret = TestSecret }
        })
        {
            using var service = new GoogleStaticMapsService(Options.Create(settings));
            Assert.False(service.IsAvailable);
            Assert.Null(service.Create("user", "Hall", "small"));
        }
    }

    [Fact]
    public void InvalidQueriesAndSizesDoNotConsumePermitsAndPerUserIssuanceIsBounded()
    {
        using var service = CreateService();
        for (var index = 0; index < 15; index++)
        {
            Assert.Null(service.Create("user", "", "small"));
            Assert.Null(service.Create("user", new string('a', 1501), "small"));
            Assert.Null(service.Create("user", "Hall", "arbitrary"));
        }
        for (var index = 0; index < 10; index++) Assert.NotNull(service.Create("user", "Hall", "small"));
        Assert.Null(service.Create("user", "Hall", "small"));
        Assert.NotNull(service.Create("other-user", "Hall", "medium"));
    }

    private static GoogleStaticMapsService CreateService() => new(Options.Create(new GoogleStaticMapsSettings
    { Enabled = true, ApiKey = "test-only", SigningSecret = TestSecret }));
}
