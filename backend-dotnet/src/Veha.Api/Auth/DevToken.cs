using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Veha.Api.Auth;

/// <summary>Выпуск локального HS256 dev-токена (порт issue_dev_token). iss="psa-dev",
/// роли в realm_access.roles — как у Keycloak. Только вне production.</summary>
public static class DevToken
{
    public const string Issuer = "psa-dev";

    public static string Issue(string username, IEnumerable<string> roles, string? email, string? name, string secret)
    {
        var realmAccess = JsonSerializer.Serialize(new { roles = roles.ToArray() });
        var claims = new List<Claim>
        {
            new("sub", $"dev:{username}"),
            new("preferred_username", username),
            new("email", string.IsNullOrEmpty(email) ? $"{username}@dev.local" : email),
            new("name", string.IsNullOrEmpty(name) ? username : name),
            new("realm_access", realmAccess, JsonClaimValueTypes.Json),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: Issuer, claims: claims, notBefore: now, expires: now.AddHours(8), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>true, если Bearer-токен в запросе выпущен dev-эмитентом ("psa-dev").</summary>
    public static bool IsDevBearer(HttpRequest request)
    {
        var auth = request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var token = auth["Bearer ".Length..].Trim();
        try { return new JwtSecurityTokenHandler().ReadJwtToken(token).Issuer == Issuer; }
        catch { return false; }
    }
}
