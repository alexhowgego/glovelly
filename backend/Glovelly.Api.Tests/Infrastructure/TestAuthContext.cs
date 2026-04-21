namespace Glovelly.Api.Tests.Infrastructure;

internal static class TestAuthContext
{
    public const string SchemeName = "Test";
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AlternateUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
}
