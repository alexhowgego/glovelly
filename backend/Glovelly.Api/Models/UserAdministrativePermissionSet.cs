using Glovelly.Api.Auth;

namespace Glovelly.Api.Models;

public sealed class UserAdministrativePermissionSet
{
    public Guid UserId { get; set; }
    public AdministrativePermissionSet PermissionSet { get; set; }
    public User User { get; set; } = null!;
}
