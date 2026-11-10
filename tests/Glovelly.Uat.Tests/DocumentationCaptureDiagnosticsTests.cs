using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class DocumentationCaptureDiagnosticsTests
{
    [Theory]
    [InlineData("https://staging.example/clients/123/map-preview?source=private&size=400x500", "client-descriptor")]
    [InlineData("https://staging.example/gigs/123/map-preview?source=private&size=400x500", "gig-descriptor")]
    [InlineData("https://maps.googleapis.com/maps/api/staticmap?key=private&signature=private", "provider-image")]
    [InlineData("https://maps.googleapis.com.evil.example/maps/api/staticmap?signature=private", null)]
    [InlineData("https://staging.example/clients/123", null)]
    [InlineData("not a URL", null)]
    public void ClassifiesRequestsWithoutReturningUrls(string url, string? expected)
    {
        Assert.Equal(expected, DocumentationCaptureTests.MapRequestKind(url));
    }
}
