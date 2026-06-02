namespace Glovelly.Api.Services;

public sealed record GoogleConnectionAccessToken(string AccessToken, string TokenType, string GrantedScopes);
