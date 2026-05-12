using Microsoft.AspNetCore.Authentication;

namespace Glovelly.Api.Auth;

public sealed class McpDevelopmentAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SectionName = "Mcp:DevelopmentAuth";
    public const string SchemeName = "McpDevelopment";

    public bool AllowAnonymous { get; set; }
}
