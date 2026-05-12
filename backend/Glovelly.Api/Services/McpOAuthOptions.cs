namespace Glovelly.Api.Services;

public sealed class McpOAuthOptions
{
    public const string SectionName = "Mcp:OAuth";

    public string? Issuer { get; set; }
    public string? Resource { get; set; }
    public int AuthorizationCodeLifetimeMinutes { get; set; } = 5;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
    public int RefreshTokenLifetimeDays { get; set; } = 30;
    public List<McpOAuthClientOptions> Clients { get; set; } = [];
}

public sealed class McpOAuthClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string DisplayName { get; set; } = "MCP client";
    public List<string> RedirectUris { get; set; } = [];
    public List<string> Scopes { get; set; } = ["mcp:read"];
}
