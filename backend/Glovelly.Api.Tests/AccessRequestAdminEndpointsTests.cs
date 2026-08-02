using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AccessRequestAdminEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public AccessRequestAdminEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Approve_ProvisionsStoredIdentityWithoutGoogleSubject_AndReportsInvitationFailure()
    {
        var request = await AddRequestAsync("requested@glovelly.local", "Requested User", DateTimeOffset.UtcNow);
        _factory.Emails.ExceptionToThrow = new InvalidOperationException("mail unavailable");

        var response = await _client.PostAsJsonAsync($"/admin/access-requests/{request.Id}/approve", new
        {
            role = "User",
            isActive = true,
            sendInvitationEmail = true
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.True(payload.GetProperty("decisionApplied").GetBoolean());
        Assert.True(payload.GetProperty("userCreated").GetBoolean());
        Assert.False(payload.GetProperty("invitationEmailSent").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(value => value.Email == "requested@glovelly.local", TestContext.Current.CancellationToken);
        var stored = await db.AccessRequests.SingleAsync(value => value.Id == request.Id, TestContext.Current.CancellationToken);
        Assert.Null(user.GoogleSubject);
        Assert.Equal(AccessRequestStatus.Provisioned, stored.Status);
        Assert.Equal(user.Id, stored.ProvisionedUserId);
    }

    [Fact]
    public async Task Approve_WithInvitation_SendsTheStandardInvitationEmail()
    {
        var request = await AddRequestAsync("invite-requested@glovelly.local", "Invite Requested", DateTimeOffset.UtcNow);

        var response = await _client.PostAsJsonAsync($"/admin/access-requests/{request.Id}/approve", new
        {
            role = "User",
            isActive = true,
            sendInvitationEmail = true
        }, TestContext.Current.CancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.GetProperty("invitationEmailSent").GetBoolean());
        var invitation = Assert.Single(_factory.Emails.SentEmails);
        Assert.Equal("invite-requested@glovelly.local", invitation.To.Single().Address);
        Assert.Contains("Accept invitation and sign in:", invitation.PlainTextBody);
        Assert.NotNull(invitation.HtmlBody);
        Assert.Contains("Accept invitation and sign in", invitation.HtmlBody);
    }

    [Fact]
    public async Task List_ExpiresStaleRequests_AndDeclineIsIdempotent()
    {
        var stale = await AddRequestAsync("stale@glovelly.local", "Stale", new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero));
        var current = await AddRequestAsync("decline@glovelly.local", "Decline", DateTimeOffset.UtcNow);

        var list = await _client.GetAsync("/admin/access-requests/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var pending = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(pending.EnumerateArray(), value => value.GetProperty("id").GetGuid() == stale.Id);

        var first = await _client.PostAsJsonAsync($"/admin/access-requests/{current.Id}/decline", new { decisionNote = "Not now" }, TestContext.Current.CancellationToken);
        var second = await _client.PostAsJsonAsync($"/admin/access-requests/{current.Id}/decline", new { decisionNote = "Again" }, TestContext.Current.CancellationToken);
        Assert.True((await first.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken)).GetProperty("decisionApplied").GetBoolean());
        Assert.False((await second.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken)).GetProperty("decisionApplied").GetBoolean());
    }

    [Fact]
    public async Task Approve_ConcurrentRequests_CreateAtMostOneUser()
    {
        var request = await AddRequestAsync("concurrent@glovelly.local", "Concurrent", DateTimeOffset.UtcNow);

        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync($"/admin/access-requests/{request.Id}/approve", new
            {
                role = "User",
                isActive = true,
                sendInvitationEmail = false
            }, TestContext.Current.CancellationToken),
            _client.PostAsJsonAsync($"/admin/access-requests/{request.Id}/approve", new
            {
                role = "User",
                isActive = true,
                sendInvitationEmail = false
            }, TestContext.Current.CancellationToken));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(value => value.Email == "concurrent@glovelly.local", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Approve_ExistingUser_DoesNotCreateDuplicate()
    {
        var request = await AddRequestAsync("existing@glovelly.local", "Existing", DateTimeOffset.UtcNow);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                DisplayName = "Existing User",
                GoogleSubject = "existing-subject",
                Role = UserRole.User,
                IsActive = true,
                CreatedUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _client.PostAsJsonAsync($"/admin/access-requests/{request.Id}/approve", new
        {
            role = "Admin",
            isActive = false,
            sendInvitationEmail = false
        }, TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.GetProperty("existingUser").GetBoolean());
        Assert.False(payload.GetProperty("userCreated").GetBoolean());

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verificationDb.Users.CountAsync(value => value.Email == request.Email, TestContext.Current.CancellationToken));
    }

    private async Task<AccessRequest> AddRequestAsync(string email, string displayName, DateTimeOffset requestedAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = new AccessRequest
        {
            Id = Guid.NewGuid(), Email = email, NormalizedEmail = email, DisplayName = displayName,
            Subject = "request-subject", RequestedAtUtc = requestedAtUtc
        };
        db.AccessRequests.Add(request);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return request;
    }
}
