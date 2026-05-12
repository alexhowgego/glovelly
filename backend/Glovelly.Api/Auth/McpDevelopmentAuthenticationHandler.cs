using System.Security.Claims;
using System.Text.Encodings.Web;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Auth;

public sealed class McpDevelopmentAuthenticationHandler(
    IOptionsMonitor<McpDevelopmentAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHostEnvironment environment,
    IConfiguration configuration,
    AppDbContext db) : AuthenticationHandler<McpDevelopmentAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment())
        {
            return AuthenticateResult.NoResult();
        }

        if (!Request.Path.StartsWithSegments("/mcp"))
        {
            return AuthenticateResult.NoResult();
        }

        if (!Options.AllowAnonymous)
        {
            return AuthenticateResult.NoResult();
        }

        var googleSubject = configuration["DevelopmentSeeding:AdminGoogleSubject"]?.Trim();
        if (string.IsNullOrWhiteSpace(googleSubject))
        {
            Logger.LogWarning("MCP development anonymous auth is disabled because no seeded admin Google subject is configured.");
            return AuthenticateResult.NoResult();
        }

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(value =>
                value.GoogleSubject == googleSubject &&
                value.IsActive &&
                value.Role == UserRole.Admin);

        if (user is null)
        {
            return AuthenticateResult.Fail("Seeded MCP development admin user does not exist or is inactive.");
        }

        var claims = new[]
        {
            new Claim(GlovellyClaimTypes.UserId, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("email", user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
