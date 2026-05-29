using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public sealed class GoogleConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? GoogleSubject { get; set; }
    public string? GoogleEmail { get; set; }
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; set; }
    public string GrantedScopes { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTimeOffset ConnectedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    [JsonIgnore]
    public GoogleDriveIntegrationSettings? DriveSettings { get; set; }
}
