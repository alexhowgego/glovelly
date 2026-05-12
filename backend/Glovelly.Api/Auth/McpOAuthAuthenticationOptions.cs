using Microsoft.AspNetCore.Authentication;

namespace Glovelly.Api.Auth;

public sealed class McpOAuthAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "McpOAuthBearer";
}
