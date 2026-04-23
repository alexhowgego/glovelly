using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

internal static class InvoiceEmailSenderIdentityBuilder
{
    public static InvoiceEmailSenderIdentity Build(User? user)
    {
        var senderName = ResolveSenderName(user);
        var fromDisplayName = $"{senderName} (via Glovelly)";
        var replyToEmail = string.IsNullOrWhiteSpace(user?.InvoiceReplyToEmail)
            ? null
            : user!.InvoiceReplyToEmail.Trim();

        return new InvoiceEmailSenderIdentity(
            fromDisplayName,
            replyToEmail,
            senderName);
    }

    private static string ResolveSenderName(User? user)
    {
        if (!string.IsNullOrWhiteSpace(user?.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(user?.Email))
        {
            return user.Email.Trim();
        }

        return "Glovelly";
    }
}
