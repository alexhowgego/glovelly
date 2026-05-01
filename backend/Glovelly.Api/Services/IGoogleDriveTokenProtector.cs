namespace Glovelly.Api.Services;

public interface IGoogleDriveTokenProtector
{
    string Protect(string token);
    string Unprotect(string encryptedToken);
}
