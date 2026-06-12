using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AdminEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public AdminEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_DoesNotSendInvitationEmailByDefault()
    {
        var createResponse = await _client.PostAsJsonAsync("/admin/users", new
        {
            email = "new-user@glovelly.local",
            displayName = "New User",
            googleSubject = (string?)null,
            mileageRate = (decimal?)null,
            passengerMileageRate = (decimal?)null,
            role = "User",
            isActive = true,
        }, TestContext.Current.CancellationToken);

        createResponse.EnsureSuccessStatusCode();

        Assert.Empty(_factory.Emails.SentEmails);
    }

    [Fact]
    public async Task SendInvitationEmail_WhenUserActive_SendsInvite()
    {
        var createResponse = await _client.PostAsJsonAsync("/admin/users", new
        {
            email = "invited-user@glovelly.local",
            displayName = "Invited User",
            googleSubject = (string?)null,
            mileageRate = (decimal?)null,
            passengerMileageRate = (decimal?)null,
            role = "User",
            isActive = true,
        }, TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var createdUser = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var userId = createdUser.GetProperty("id").GetGuid();

        var inviteResponse = await _client.PostAsync($"/admin/users/{userId}/invitation-email", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, inviteResponse.StatusCode);
        var email = Assert.Single(_factory.Emails.SentEmails);
        var recipient = Assert.Single(email.To);
        Assert.Equal("invited-user@glovelly.local", recipient.Address);
        Assert.Equal("Invited User", recipient.DisplayName);
        Assert.Equal("You have been invited to Glovelly", email.Subject);
        Assert.Contains("Sign in with Google using this email address", email.PlainTextBody);
        Assert.Contains("/auth/login", email.PlainTextBody);
    }

    [Fact]
    public async Task SendInvitationEmail_WhenUserMissing_ReturnsNotFound()
    {
        var inviteResponse = await _client.PostAsync($"/admin/users/{Guid.NewGuid()}/invitation-email", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, inviteResponse.StatusCode);
        Assert.Empty(_factory.Emails.SentEmails);
    }

    [Fact]
    public async Task DeleteUser_WhenInactive_DeletesUser()
    {
        var createResponse = await _client.PostAsJsonAsync("/admin/users", new
        {
            email = "inactive-user@glovelly.local",
            displayName = "Inactive User",
            googleSubject = (string?)null,
            mileageRate = (decimal?)null,
            passengerMileageRate = (decimal?)null,
            role = "User",
            isActive = false,
        }, TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var createdUser = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var userId = createdUser.GetProperty("id").GetGuid();

        var deleteResponse = await _client.DeleteAsync($"/admin/users/{userId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await _client.GetAsync("/admin/users", TestContext.Current.CancellationToken);
        listResponse.EnsureSuccessStatusCode();
        var users = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            users.EnumerateArray(),
            user => user.GetProperty("id").GetGuid() == userId);
    }

    [Fact]
    public async Task DeleteUser_WhenActive_ReturnsValidationProblem()
    {
        var createResponse = await _client.PostAsJsonAsync("/admin/users", new
        {
            email = "active-user@glovelly.local",
            displayName = "Active User",
            googleSubject = (string?)null,
            mileageRate = (decimal?)null,
            passengerMileageRate = (decimal?)null,
            role = "User",
            isActive = true,
        }, TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var createdUser = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var userId = createdUser.GetProperty("id").GetGuid();

        var deleteResponse = await _client.DeleteAsync($"/admin/users/{userId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        var problem = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Only inactive users can be deleted.",
            problem.GetProperty("errors").GetProperty("isActive")[0].GetString());

        var listResponse = await _client.GetAsync("/admin/users", TestContext.Current.CancellationToken);
        listResponse.EnsureSuccessStatusCode();
        var users = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Contains(
            users.EnumerateArray(),
            user => user.GetProperty("id").GetGuid() == userId);
    }

    [Fact]
    public async Task DeleteUser_WhenCurrentUser_ReturnsValidationProblem()
    {
        var deleteResponse = await _client.DeleteAsync($"/admin/users/{TestAuthContext.UserId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        var problem = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(
            "You cannot delete your own administrator account.",
            problem.GetProperty("errors").GetProperty("id")[0].GetString());
    }
}
