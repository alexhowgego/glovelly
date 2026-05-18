using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AdminEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public AdminEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
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
        });
        createResponse.EnsureSuccessStatusCode();
        var createdUser = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var userId = createdUser.GetProperty("id").GetGuid();

        var deleteResponse = await _client.DeleteAsync($"/admin/users/{userId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await _client.GetAsync("/admin/users");
        listResponse.EnsureSuccessStatusCode();
        var users = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
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
        });
        createResponse.EnsureSuccessStatusCode();
        var createdUser = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var userId = createdUser.GetProperty("id").GetGuid();

        var deleteResponse = await _client.DeleteAsync($"/admin/users/{userId}");

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        var problem = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Only inactive users can be deleted.",
            problem.GetProperty("errors").GetProperty("isActive")[0].GetString());

        var listResponse = await _client.GetAsync("/admin/users");
        listResponse.EnsureSuccessStatusCode();
        var users = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Contains(
            users.EnumerateArray(),
            user => user.GetProperty("id").GetGuid() == userId);
    }

    [Fact]
    public async Task DeleteUser_WhenCurrentUser_ReturnsValidationProblem()
    {
        var deleteResponse = await _client.DeleteAsync($"/admin/users/{TestAuthContext.UserId}");

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        var problem = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "You cannot delete your own administrator account.",
            problem.GetProperty("errors").GetProperty("id")[0].GetString());
    }
}
