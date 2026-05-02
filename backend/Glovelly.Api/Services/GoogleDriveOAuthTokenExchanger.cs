using System.Text.Json;

namespace Glovelly.Api.Services;

public sealed class GoogleDriveOAuthTokenExchanger(HttpClient httpClient) : IGoogleDriveOAuthTokenExchanger
{
    private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GoogleDriveOAuthTokenExchangeResult> ExchangeCodeAsync(
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
        GoogleDriveOAuthTokenResponse? tokenResponse = null;
        if (response.IsSuccessStatusCode)
        {
            try
            {
                tokenResponse = JsonSerializer.Deserialize<GoogleDriveOAuthTokenResponse>(
                    responseBody,
                    JsonOptions);
            }
            catch (JsonException)
            {
                tokenResponse = null;
            }
        }

        return new GoogleDriveOAuthTokenExchangeResult(
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            responseBody,
            tokenResponse);
    }
}
