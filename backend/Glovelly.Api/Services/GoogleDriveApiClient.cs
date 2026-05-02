using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glovelly.Api.Services;

public sealed class GoogleDriveApiClient(HttpClient httpClient) : IGoogleDriveApiClient
{
    private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string GoogleDriveUploadEndpoint =
        "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,webViewLink";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GoogleDriveAccessTokenRefreshResult> RefreshAccessTokenAsync(
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

        return new GoogleDriveAccessTokenRefreshResult(
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            responseBody,
            tokenResponse);
    }

    public async Task<GoogleDriveUploadResult> UploadPdfAsync(
        string accessToken,
        string fileName,
        byte[] content,
        string? parentFolderId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GoogleDriveUploadEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = BuildMultipartContent(fileName, content, parentFolderId);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var folderHint = string.IsNullOrWhiteSpace(parentFolderId)
                ? string.Empty
                : " Check that the configured Google Drive folder ID exists and is accessible.";
            throw new InvalidOperationException(
                $"Google Drive upload failed with HTTP {(int)response.StatusCode}.{folderHint} {responseBody}".Trim());
        }

        var uploadResponse = JsonSerializer.Deserialize<GoogleDriveUploadResponse>(
            responseBody,
            JsonOptions);
        if (uploadResponse is null || string.IsNullOrWhiteSpace(uploadResponse.Id))
        {
            throw new InvalidOperationException("Google Drive upload response did not include a file id.");
        }

        return new GoogleDriveUploadResult(
            uploadResponse.Id,
            uploadResponse.Name ?? fileName,
            uploadResponse.WebViewLink);
    }

    private static MultipartContent BuildMultipartContent(
        string fileName,
        byte[] content,
        string? parentFolderId)
    {
        var multipart = new MultipartContent("related");
        object metadataValue = string.IsNullOrWhiteSpace(parentFolderId)
            ? new
            {
                name = fileName,
                mimeType = "application/pdf",
            }
            : new
            {
                name = fileName,
                mimeType = "application/pdf",
                parents = new[] { parentFolderId.Trim() },
            };

        var metadata = JsonSerializer.Serialize(
            metadataValue,
            JsonOptions);

        multipart.Add(new StringContent(metadata, Encoding.UTF8, "application/json"));
        multipart.Add(new ByteArrayContent(content)
        {
            Headers =
            {
                ContentType = new MediaTypeHeaderValue("application/pdf"),
            },
        });

        return multipart;
    }

    private sealed class GoogleDriveUploadResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("webViewLink")]
        public string? WebViewLink { get; set; }
    }
}
