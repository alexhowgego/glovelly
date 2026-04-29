using Microsoft.AspNetCore.DataProtection;

namespace Glovelly.Api.Services;

public sealed class GoogleDriveTokenProtector(IDataProtectionProvider dataProtectionProvider)
    : IGoogleDriveTokenProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("Glovelly.GoogleDriveTokens.v1");

    public string Protect(string token)
    {
        return _protector.Protect(token);
    }

    public string Unprotect(string encryptedToken)
    {
        return _protector.Unprotect(encryptedToken);
    }
}
