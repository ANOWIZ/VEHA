using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Veha.Api.Auth;

/// <summary>Dev-аутентификация по заголовку «X-Dev-User: username[:role1,role2]».
/// Порт dev-fallback из Python (principal_from_dev_header). Включается только когда
/// AuthDevMode и окружение не production (см. ForwardDefaultSelector в Program).</summary>
public class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dev";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers["X-Dev-User"].ToString();
        if (string.IsNullOrWhiteSpace(header))
            return Task.FromResult(AuthenticateResult.NoResult());

        var parts = header.Split(':', 2);
        var username = parts[0].Trim();
        var roles = parts.Length > 1
            ? parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : ["engineer"];

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new("preferred_username", username),
            new("email", $"{username}@dev.local"),
            new("name", username),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName)));
    }
}
