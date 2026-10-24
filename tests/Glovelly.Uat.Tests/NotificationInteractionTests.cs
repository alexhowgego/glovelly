using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class NotificationInteractionTests : InvoiceUatTestBase
{
    [Theory]
    [InlineData(1280, 800)]
    [InlineData(390, 844)]
    public Task PersistentNotificationsKeepProfileAndDismissControlsUsable(int width, int height) => RunWithDiagnosticsAsync(
        $"{nameof(PersistentNotificationsKeepProfileAndDismissControlsUsable)}_{width}", async () =>
        {
            await Page.SetViewportSizeAsync(width, height);
            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync($"{CreateRunId()} Notification Client");
            await Page.RouteAsync("**/clients/*", route => route.Request.Method == "DELETE"
                ? route.FulfillAsync(new()
                {
                    Status = 500,
                    ContentType = "application/problem+json",
                    Body = "{\"title\":\"Notification interaction fixture\",\"status\":500}"
                })
                : route.FallbackAsync());
            IResponse? deleteResponse = null;
            await RunAndAcceptConfirmAsync(async () =>
            {
                deleteResponse = await Page.RunAndWaitForResponseAsync(
                    () => Page.Locator(".record-overview").GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync(),
                    response => response.Request.Method == "DELETE" && new Uri(response.Url).AbsolutePath.StartsWith("/clients/", StringComparison.Ordinal));
            });
            Assert.NotNull(deleteResponse);
            Assert.Equal(500, deleteResponse.Status);
            await ExpectNotificationAsync("Notification interaction fixture", "error");
            var error = Page.Locator("[data-sonner-toast][data-type='error']").Filter(new() { HasText = "Notification interaction fixture" });
            Assert.Equal(width > 600 ? "none" : "auto", await error.EvaluateAsync<string>("el => getComputedStyle(el).pointerEvents"));
            if (width > 600)
                Assert.Equal("24px", await Page.Locator("[data-sonner-toaster]").EvaluateAsync<string>("el => getComputedStyle(el).top"));

            var profile = Page.GetByTestId("profile-menu-button");
            await profile.ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Menuitem, new() { Name = "Settings", Exact = true })).ToBeVisibleAsync();
            await profile.PressAsync("Enter");
            var dismiss = error.GetByRole(AriaRole.Button, new() { Name = "Dismiss notification" });
            Assert.Equal("auto", await dismiss.EvaluateAsync<string>("el => getComputedStyle(el).pointerEvents"));
            await dismiss.FocusAsync();
            await dismiss.ClickAsync();
            await Assertions.Expect(error).ToHaveCountAsync(0);
        });
}
