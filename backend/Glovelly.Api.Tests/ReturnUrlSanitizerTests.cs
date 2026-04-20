using Glovelly.Api.Auth;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class ReturnUrlSanitizerTests
{
    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData(" ", "/")]
    [InlineData("/dashboard", "/dashboard")]
    [InlineData("/invoices?status=draft", "/invoices?status=draft")]
    [InlineData("/gigs#current", "/gigs#current")]
    [InlineData("https://example.com/account", "/")]
    [InlineData("//evil.example.com", "/")]
    [InlineData("javascript:alert('x')", "/")]
    public void BuildSafeLocalReturnPath_SanitizesUnsafeValues(string? returnUrl, string expected)
    {
        var actual = ReturnUrlSanitizer.BuildSafeLocalReturnPath(returnUrl);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("/safe\r\nX-Injected: yes")]
    [InlineData("/safe\nX-Injected: yes")]
    public void BuildSafeLocalReturnPath_BlocksHeaderInjection(string returnUrl)
    {
        var actual = ReturnUrlSanitizer.BuildSafeLocalReturnPath(returnUrl);

        Assert.Equal("/", actual);
    }
}
