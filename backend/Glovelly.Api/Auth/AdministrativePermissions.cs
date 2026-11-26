namespace Glovelly.Api.Auth;

public static class AdministrativePermissions
{
    public const string ManageUsers = "ManageUsers";
    public const string ManageLicences = "ManageLicences";
    public const string ManageSystem = "ManageSystem";
    public const string ManagePermissions = "ManagePermissions";
}

public enum AdministrativePermissionSet
{
    UserAdministrator,
    LicenseAdministrator,
    SuperAdministrator,
}

public static class AdministrativePermissionSets
{
    public static IReadOnlySet<string> Resolve(IEnumerable<AdministrativePermissionSet> permissionSets)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permissionSet in permissionSets)
        {
            switch (permissionSet)
            {
                case AdministrativePermissionSet.UserAdministrator:
                    permissions.Add(AdministrativePermissions.ManageUsers);
                    break;
                case AdministrativePermissionSet.LicenseAdministrator:
                    permissions.Add(AdministrativePermissions.ManageLicences);
                    break;
                case AdministrativePermissionSet.SuperAdministrator:
                    permissions.Add(AdministrativePermissions.ManageUsers);
                    permissions.Add(AdministrativePermissions.ManageLicences);
                    permissions.Add(AdministrativePermissions.ManageSystem);
                    permissions.Add(AdministrativePermissions.ManagePermissions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(permissionSets), permissionSet, "Unknown administrative permission set.");
            }
        }

        return permissions;
    }
}
