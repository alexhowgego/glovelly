using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Glovelly.Api.Services;

public sealed class GoogleDocReadException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed partial class GoogleDocIntakeReader(IGoogleConnectionService connections, IHttpClientFactory clients, ILogger<GoogleDocIntakeReader>? logger = null)
{
    [GeneratedRegex("^/document/d/([A-Za-z0-9_-]{1,200})(?:/.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentPath();

    public static bool TryGetDocumentId(string? url, out string id)
    {
        id = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "docs.google.com", StringComparison.OrdinalIgnoreCase) || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        var match = DocumentPath().Match(uri.AbsolutePath);
        if (!match.Success) return false;
        id = match.Groups[1].Value;
        return true;
    }

    public async Task<string> ReadAsync(Guid userId, string id, CancellationToken token)
    {
        var connection = await connections.GetActiveConnectionAsync(userId, [GoogleScopes.DocumentsReadonly], token)
            ?? throw new GoogleDocReadException("document_connection_required", "Connect Google Docs in Services to read this document, or attach the link as a resource.");
        var access = await connections.GetAccessTokenAsync(connection, [GoogleScopes.DocumentsReadonly], token);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://docs.googleapis.com/v1/documents/{Uri.EscapeDataString(id)}?includeTabsContent=true");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
        using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode)
        {
            logger?.LogWarning("Google Docs intake read failed with HTTP status {StatusCode}.", (int)response.StatusCode);
            throw new GoogleDocReadException("document_access_failed", "Google Docs could not read this document. Check the connected account, document access and Google Docs API availability, then retry.");
        }
        if (response.Content.Headers.ContentLength > 2_000_000) throw new GoogleDocReadException("document_too_large", "This document is too large to analyse. Upload a PDF or paste a shorter excerpt.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, token)) != 0)
        {
            if (buffer.Length + count > 2_000_000) throw new GoogleDocReadException("document_too_large", "This document is too large to analyse. Upload a PDF or paste a shorter excerpt.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), token);
        }
        buffer.Position = 0;
        using var json = await JsonDocument.ParseAsync(buffer, cancellationToken: token);
        var text = new StringBuilder();
        AppendText(json.RootElement, text);
        if (text.Length == 0) throw new GoogleDocReadException("document_empty", "This document contains no readable text. Upload a PDF or paste an excerpt.");
        return text.ToString();
    }

    private static void AppendText(JsonElement element, StringBuilder text)
    {
        if (element.ValueKind == JsonValueKind.Array) { foreach (var item in element.EnumerateArray()) AppendText(item, text); return; }
        if (element.ValueKind != JsonValueKind.Object) return;
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals("textRun") && property.Value.ValueKind == JsonValueKind.Object && property.Value.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            {
                var value = content.GetString() ?? "";
                if (text.Length + value.Length > 16_000) throw new GoogleDocReadException("document_too_large", "This document is too long to analyse. Upload a PDF or paste a shorter excerpt.");
                text.Append(value);
            }
            else AppendText(property.Value, text);
        }
    }
}
