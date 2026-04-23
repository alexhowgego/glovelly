using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
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
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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

        var response = await _client.PostAsJsonAsync("/access/request", new
        {
            accessRequestToken = CreateAccessRequestToken(
                "new-user@glovelly.local",
                "New User",
                "google-sub-new-user"),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Access request submitted.", await ReadMessageAsync(response));
        Assert.Collection(
            _factory.Emails.SentEmails.OrderBy(value => value.To.Single().Address),
            message =>
            {
                Assert.Equal("second-admin@glovelly.local", message.To.Single().Address);
                Assert.Equal("Glovelly access request", message.Subject);
                Assert.Equal("access@glovelly.test", message.From?.Address);
                Assert.Equal("Glovelly Access", message.From?.DisplayName);
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
                Assert.Equal("access@glovelly.test", message.From?.Address);
                Assert.Equal("Glovelly Access", message.From?.DisplayName);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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

        var response = await _client.PostAsJsonAsync("/access/request", new
        {
            accessRequestToken = CreateAccessRequestToken(
                "new-user@glovelly.local",
                "New User",
                "google-sub-new-user"),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Access request submitted.", await ReadMessageAsync(response));
        Assert.Single(_factory.Emails.SentEmails);
        Assert.Equal("test-admin@glovelly.local", _factory.Emails.SentEmails[0].To.Single().Address);
        Assert.Equal("access@glovelly.test", _factory.Emails.SentEmails[0].From?.Address);
    }

    [Fact]
    public async Task RequestAccess_ReturnsBadRequest_WhenEmailClaimMissing()
    {
        var response = await _client.PostAsJsonAsync("/access/request", new
        {
            accessRequestToken = CreateAccessRequestToken(
                "   ",
                "New User",
                "google-sub-new-user"),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Emails.SentEmails);
    }

    [Fact]
    public async Task RequestAccess_ReturnsProblem_WhenEmailDispatchFails()
    {
        _factory.Emails.ExceptionToThrow = new InvalidOperationException("SMTP unavailable");

        var response = await _client.PostAsJsonAsync("/access/request", new
        {
            accessRequestToken = CreateAccessRequestToken(
                "new-user@glovelly.local",
                "New User",
                "google-sub-new-user"),
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Unable to submit access request", problem.GetProperty("title").GetString());
        Assert.Equal(
            "We couldn't submit your access request right now. Please try again shortly.",
            problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task RequestAccess_SuppressesRepeatNotificationsForSameEmailWithinWindow()
    {
        var firstRequest = CreateAccessRequestWithToken("repeat-user@glovelly.local", "198.51.100.14");
        var secondRequest = CreateAccessRequestWithToken("REPEAT-USER@glovelly.local", "198.51.100.14");

        var firstResponse = await _client.SendAsync(firstRequest);
        var secondResponse = await _client.SendAsync(secondRequest);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal("Access request submitted.", await ReadMessageAsync(firstResponse));
        Assert.Equal("Access request submitted.", await ReadMessageAsync(secondResponse));
        Assert.Single(_factory.Emails.SentEmails);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedRequests = await dbContext.AccessRequests
            .OrderBy(value => value.RequestedAtUtc)
            .ToListAsync();

        Assert.Equal(2, storedRequests.Count);
        Assert.NotNull(storedRequests[0].NotificationSentAtUtc);
        Assert.Null(storedRequests[0].NotificationSuppressionReason);
        Assert.Null(storedRequests[1].NotificationSentAtUtc);
        Assert.Equal(
            AccessRequestWorkflowService.DuplicateEmailSuppressionReason,
            storedRequests[1].NotificationSuppressionReason);
    }

    [Fact]
    public async Task RequestAccess_SuppressesNotificationWhenDailyCapReached()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;

            for (var index = 0; index < 50; index++)
            {
                dbContext.AccessRequests.Add(new AccessRequest
                {
                    Id = Guid.NewGuid(),
                    Email = $"existing-{index}@glovelly.local",
                    NormalizedEmail = $"existing-{index}@glovelly.local",
                    RequestedAtUtc = now.AddMinutes(-index),
                    NotificationSentAtUtc = now.AddMinutes(-index)
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var request = CreateAccessRequestWithToken("quota-target@glovelly.local", "198.51.100.15");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Access request submitted.", await ReadMessageAsync(response));
        Assert.Empty(_factory.Emails.SentEmails);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedRequest = await verificationDbContext.AccessRequests
            .OrderByDescending(value => value.RequestedAtUtc)
            .FirstAsync(value => value.NormalizedEmail == "quota-target@glovelly.local");

        Assert.Null(storedRequest.NotificationSentAtUtc);
        Assert.Equal(
            AccessRequestWorkflowService.DailyNotificationCapSuppressionReason,
            storedRequest.NotificationSuppressionReason);
    }

    [Fact]
    public async Task RequestAccess_ReturnsGenericSuccess_WhenRateLimited()
    {
        var responses = new List<HttpResponseMessage>();

        for (var index = 0; index < 6; index++)
        {
            var request = CreateAccessRequestWithToken($"rate-limit-{index}@glovelly.local", "198.51.100.16");
            responses.Add(await _client.SendAsync(request));
        }

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        foreach (var response in responses)
        {
            Assert.Equal("Access request submitted.", await ReadMessageAsync(response));
        }

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(5, await dbContext.AccessRequests.CountAsync());
        Assert.Equal(5, _factory.Emails.SentEmails.Count);
    }

    [Fact]
    public async Task RequestAccess_AllowsAnonymousSubmissionWithValidAccessRequestToken()
    {
        var token = CreateAccessRequestToken("new-user@glovelly.local", "New User", "google-sub-new-user");

        var response = await _client.PostAsJsonAsync("/access/request", new
        {
            accessRequestToken = token,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(_factory.Emails.SentEmails);
        Assert.Equal("test-admin@glovelly.local", _factory.Emails.SentEmails[0].To.Single().Address);
        Assert.Equal("access@glovelly.test", _factory.Emails.SentEmails[0].From?.Address);
        Assert.Contains("User email: new-user@glovelly.local", _factory.Emails.SentEmails[0].PlainTextBody);
        Assert.Contains("User display name: New User", _factory.Emails.SentEmails[0].PlainTextBody);
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return payload.GetProperty("message").GetString();
    }

    private HttpRequestMessage CreateAccessRequestWithToken(string email, string remoteIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/access/request");
        request.Headers.Add("X-Test-Remote-Ip", remoteIp);
        request.Content = JsonContent.Create(new
        {
            accessRequestToken = CreateAccessRequestToken(email, "New User", "google-sub-new-user"),
        });
        return request;
    }

    private string CreateAccessRequestToken(string email, string displayName, string subject)
    {
        using var scope = _factory.Services.CreateScope();
        var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider
            .CreateProtector("Glovelly.AccessRequest")
            .ToTimeLimitedDataProtector();

        return protector.Protect(
            JsonSerializer.Serialize(new
            {
                email,
                displayName,
                subject,
            }),
            lifetime: TimeSpan.FromMinutes(15));
    }
}
