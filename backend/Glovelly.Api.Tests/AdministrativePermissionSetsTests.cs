using Glovelly.Api.Auth;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AdministrativePermissionSetsTests
{
    [Fact]
    public void IndependentSetsAreAdditiveWithoutSuperPermissions()
    {
        var permissions = AdministrativePermissionSets.Resolve([
            AdministrativePermissionSet.UserAdministrator,
            AdministrativePermissionSet.LicenseAdministrator
        ]);

        Assert.Contains(AdministrativePermissions.ManageUsers, permissions);
        Assert.Contains(AdministrativePermissions.ManageLicences, permissions);
        Assert.DoesNotContain(AdministrativePermissions.ManagePermissions, permissions);
        Assert.DoesNotContain(AdministrativePermissions.ManageSystem, permissions);
    }

    [Fact]
    public void SuperAdministratorHasExplicitCapabilities()
    {
        var permissions = AdministrativePermissionSets.Resolve([AdministrativePermissionSet.SuperAdministrator]);

        Assert.Equal(4, permissions.Count);
        Assert.Contains(AdministrativePermissions.ManageUsers, permissions);
        Assert.Contains(AdministrativePermissions.ManageLicences, permissions);
        Assert.Contains(AdministrativePermissions.ManageSystem, permissions);
        Assert.Contains(AdministrativePermissions.ManagePermissions, permissions);
    }

    [Fact]
    public void UnknownPermissionSetIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AdministrativePermissionSets.Resolve([(AdministrativePermissionSet)999]));
    }
}
