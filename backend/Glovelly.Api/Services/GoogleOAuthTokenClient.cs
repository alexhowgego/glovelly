using System.Text.Json;

namespace Glovelly.Api.Services;

public sealed class GoogleOAuthTokenClient(HttpClient httpClient) : IGoogleOAuthTokenClient
{
    private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GoogleOAuthTokenExchangeResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GoogleTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
            }),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        return new GoogleOAuthTokenExchangeResult(
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            responseBody,
            DeserializeTokenResponse(response.IsSuccessStatusCode, responseBody));
    }

    public async Task<GoogleOAuthTokenRefreshResult> RefreshAccessTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GoogleTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
            }),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        return new GoogleOAuthTokenRefreshResult(
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            responseBody,
            DeserializeTokenResponse(response.IsSuccessStatusCode, responseBody));
    }

    private static GoogleOAuthTokenResponse? DeserializeTokenResponse(bool isSuccess, string responseBody)
    {
        if (!isSuccess)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GoogleOAuthTokenResponse>(responseBody, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
