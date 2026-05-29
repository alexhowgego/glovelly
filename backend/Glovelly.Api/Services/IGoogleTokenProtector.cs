namespace Glovelly.Api.Services;

public interface IGoogleTokenProtector
{
    string Protect(string token);
    string Unprotect(string encryptedToken);
}
