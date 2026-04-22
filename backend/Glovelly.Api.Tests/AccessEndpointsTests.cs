using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AccessEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public AccessEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RequestAccess_SendsNotificationToEachAdministratorWithEmail()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Glovelly.Api.Data.AppDbContext>();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "second-admin@glovelly.local",
            DisplayName = "Second Admin",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        });
        await dbContext.SaveChangesAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/access/request");
        request.Headers.Add("X-Test-Include-UserId", "false");
        request.Headers.Add("X-Test-Email", "new-user@glovelly.local");
        request.Headers.Add("X-Test-Name", "New User");
        request.Headers.Add("X-Test-Subject", "google-sub-new-user");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Collection(
            _factory.Emails.SentEmails.OrderBy(value => value.To.Single().Address),
            message =>
            {
                Assert.Equal("second-admin@glovelly.local", message.To.Single().Address);
                Assert.Equal("Glovelly access request", message.Subject);
                Assert.Contains("Environment: Testing", message.PlainTextBody);
                Assert.Contains("User email: new-user@glovelly.local", message.PlainTextBody);
                Assert.Contains("User display name: New User", message.PlainTextBody);
                Assert.Contains("Timestamp:", message.PlainTextBody);
                Assert.Contains("Identity subject: google-sub-new-user", message.PlainTextBody);
                Assert.NotNull(message.HtmlBody);
                Assert.Contains("Glovelly", message.HtmlBody);
                Assert.Contains("Testing", message.HtmlBody);
            },
            message =>
            {
                Assert.Equal("test-admin@glovelly.local", message.To.Single().Address);
                Assert.Equal("Glovelly access request", message.Subject);
                Assert.Contains("Environment: Testing", message.PlainTextBody);
                Assert.Contains("User email: new-user@glovelly.local", message.PlainTextBody);
                Assert.Contains("User display name: New User", message.PlainTextBody);
                Assert.Contains("Timestamp:", message.PlainTextBody);
                Assert.Contains("Identity subject: google-sub-new-user", message.PlainTextBody);
                Assert.NotNull(message.HtmlBody);
                Assert.Contains("Glovelly", message.HtmlBody);
                Assert.Contains("Testing", message.HtmlBody);
            });
    }

    [Fact]
    public async Task RequestAccess_SkipsAdministratorsWithoutEmail()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Glovelly.Api.Data.AppDbContext>();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "   ",
            DisplayName = "Missing Email Admin",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        });
        await dbContext.SaveChangesAsync();

        var response = await _client.PostAsync("/access/request", JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(_factory.Emails.SentEmails);
        Assert.Equal("test-admin@glovelly.local", _factory.Emails.SentEmails[0].To.Single().Address);
    }

    [Fact]
    public async Task RequestAccess_ReturnsBadRequest_WhenEmailClaimMissing()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/access/request");
        request.Headers.Add("X-Test-Email", "   ");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Emails.SentEmails);
    }

    [Fact]
    public async Task RequestAccess_ReturnsProblem_WhenEmailDispatchFails()
    {
        _factory.Emails.ExceptionToThrow = new InvalidOperationException("SMTP unavailable");

        var response = await _client.PostAsync("/access/request", JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Unable to submit access request", problem.GetProperty("title").GetString());
        Assert.Equal(
            "We couldn't submit your access request right now. Please try again shortly.",
            problem.GetProperty("detail").GetString());
    }
}
