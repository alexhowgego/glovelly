using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

internal static class GigResourceValidation
{
    public static Dictionary<string, string[]> Validate(GigExternalResourceType type, GigExternalResourcePurpose purpose, string? title, string? url, string? notes)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.IsDefined(type)) errors["resourceType"] = ["Resource type is invalid."];
        if (!Enum.IsDefined(purpose)) errors["purpose"] = ["Purpose is invalid."];
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) errors["title"] = ["A title of 200 characters or fewer is required."];
        if (notes?.Length > 2000) errors["notes"] = ["Notes must be 2,000 characters or fewer."];
        if (!string.IsNullOrWhiteSpace(url) && (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            errors["url"] = ["URL must be an absolute http or https URL."];
        else if (url?.Length > 2000) errors["url"] = ["URL must be 2,000 characters or fewer."];
        return errors;
    }
}
