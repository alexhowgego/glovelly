using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

const string devCorsPolicy = "FrontendDevelopment";
var googleSection = builder.Configuration.GetSection("Authentication:Google");
var googleClientId = googleSection["ClientId"];
var googleClientSecret = googleSection["ClientSecret"];
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var glovellyConnectionString = builder.Configuration.GetConnectionString("Glovelly");
var usePostgres = !string.IsNullOrWhiteSpace(glovellyConnectionString);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgres)
    {
        options.UseNpgsql(glovellyConnectionString);
        return;
    }

    options.UseInMemoryDatabase("Glovelly");
});
builder.Services.AddCors(options =>
{
    options.AddPolicy(devCorsPolicy, policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});
builder.Services.AddAuthorization();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/denied";
        options.Cookie.Name = "glovelly.auth";
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (IsApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (IsApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
        };
    })
    .AddOpenIdConnect(options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Authority = "https://accounts.google.com";
        options.ClientId = googleClientId ?? string.Empty;
        options.ClientSecret = googleClientSecret ?? string.Empty;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.CallbackPath = "/signin-oidc";
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (usePostgres)
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
    else
    {
        await AppDbSeeder.SeedAsync(dbContext);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseHttpsRedirection();
    app.UseCors(devCorsPolicy);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();

var auth = app.MapGroup("/auth").AllowAnonymous();

auth.MapGet("/login", (HttpContext httpContext, string? returnUrl) =>
{
    if (string.IsNullOrWhiteSpace(googleClientId) || string.IsNullOrWhiteSpace(googleClientSecret))
    {
        return Results.Problem(
            detail: "Google OIDC is not configured. Set Authentication:Google:ClientId and ClientSecret.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    var redirectUri = BuildSafeRedirectUri(httpContext, returnUrl);
    return Results.Challenge(
        new AuthenticationProperties { RedirectUri = redirectUri },
        authenticationSchemes: [OpenIdConnectDefaults.AuthenticationScheme]);
});

auth.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

auth.MapGet("/me", [Authorize] (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        name = user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name) ?? "Signed in user",
        email = user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
    });
});

auth.MapGet("/denied", () => Results.Problem(
    detail: "You do not have access to this application.",
    statusCode: StatusCodes.Status403Forbidden));

app.MapCrudEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

static string BuildSafeRedirectUri(HttpContext httpContext, string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl))
    {
        return "/";
    }

    if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteUri))
    {
        var request = httpContext.Request;
        var sameHost = string.Equals(absoluteUri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase);
        var localhostRedirect =
            absoluteUri.IsLoopback &&
            (absoluteUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             absoluteUri.Host.Equals("127.0.0.1"));

        if (sameHost || localhostRedirect)
        {
            return absoluteUri.ToString();
        }
    }

    return returnUrl.StartsWith('/') ? returnUrl : "/";
}

static bool IsApiRequest(HttpRequest request)
{
    var path = request.Path;

    return path.StartsWithSegments("/auth/me") ||
           path.StartsWithSegments("/clients") ||
           path.StartsWithSegments("/gigs") ||
           path.StartsWithSegments("/invoices") ||
           path.StartsWithSegments("/invoice-lines");
}
