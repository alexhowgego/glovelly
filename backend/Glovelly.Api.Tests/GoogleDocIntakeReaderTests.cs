using Glovelly.Api.Services;
using Glovelly.Api.Models;
using System.Net;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GoogleDocIntakeReaderTests
{
    private sealed class Connections(bool enabled) : IGoogleConnectionService
    {
        public Task<GoogleConnection> SaveConnectionAsync(Guid userId, GoogleOAuthTokenResponse tokenResponse, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<GoogleConnection?> GetActiveConnectionAsync(Guid userId, IEnumerable<string> requiredScopes, CancellationToken cancellationToken)
        {
            Assert.Contains(GoogleScopes.DocumentsReadonly, requiredScopes);
            return Task.FromResult(enabled ? new GoogleConnection() : null);
        }
        public Task<GoogleConnectionAccessToken> GetAccessTokenAsync(GoogleConnection connection, IEnumerable<string> requiredScopes, CancellationToken cancellationToken) => Task.FromResult(new GoogleConnectionAccessToken("test-token", "Bearer", GoogleScopes.DocumentsReadonly));
        public bool HasScopes(GoogleConnection connection, IEnumerable<string> requiredScopes) => enabled;
    }
    private sealed class Handler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("docs.googleapis.com", request.RequestUri!.Host);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
        }
    }
    private sealed class Clients(string response) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(response));
    }

    [Theory]
    [InlineData("https://docs.google.com/document/d/abc_123-xyz/edit", true)]
    [InlineData("https://docs.google.com/document/d/abc_123-xyz", true)]
    [InlineData("https://docs.google.com/spreadsheets/d/abc/edit", false)]
    [InlineData("https://docs.google.com.evil.test/document/d/abc/edit", false)]
    [InlineData("http://docs.google.com/document/d/abc/edit", false)]
    [InlineData("https://user@docs.google.com/document/d/abc/edit", false)]
    public void AcceptsOnlyGoogleDocumentUrls(string url, bool expected) => Assert.Equal(expected, GoogleDocIntakeReader.TryGetDocumentId(url, out _));

    [Fact]
    public async Task Read_ExtractsBoundedTextUsingAuthorisedConnection()
    {
        var reader = new GoogleDocIntakeReader(new Connections(true), new Clients("""{"tabs":[{"documentTab":{"body":{"content":[{"paragraph":{"elements":[{"textRun":{"content":"Booking\n"}},{"textRun":{"content":"Venue"}}]}}]}}}]}"""));
        Assert.Equal("Booking\nVenue", await reader.ReadAsync(Guid.NewGuid(), "abc", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Read_RejectsMissingPermissionAndOversizedContent()
    {
        var missing = new GoogleDocIntakeReader(new Connections(false), new Clients("{}"));
        await Assert.ThrowsAsync<GoogleDocReadException>(() => missing.ReadAsync(Guid.NewGuid(), "abc", TestContext.Current.CancellationToken));
        var large = new GoogleDocIntakeReader(new Connections(true), new Clients("{\"textRun\":{\"content\":\"" + new string('x', 16001) + "\"}}"));
        await Assert.ThrowsAsync<GoogleDocReadException>(() => large.ReadAsync(Guid.NewGuid(), "abc", TestContext.Current.CancellationToken));
    }
}
