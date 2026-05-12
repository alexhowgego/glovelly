namespace Glovelly.Api.Models;

public sealed class McpOAuthAccessToken
{
    public Guid Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
    public User? User { get; set; }
}
