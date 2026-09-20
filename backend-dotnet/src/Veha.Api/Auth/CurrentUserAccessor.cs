using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Infrastructure;

namespace Veha.Api.Auth;

/// <summary>Текущий пользователь: разбор claims + JIT-provisioning в БД (порт
/// get_current_user + UserService.sync_from_principal). Даёт доменного User с Id/ролями/
/// client_id для проверок доступа. Кэшируется в пределах запроса (scoped).</summary>
public class CurrentUserAccessor(IHttpContextAccessor http, VehaDbContext db)
{
    private User? _cached;

    public async Task<User> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;

        var principal = http.HttpContext?.User
            ?? throw new UnauthenticatedException("Требуется аутентификация");

        var username = principal.FindFirst("preferred_username")?.Value
            ?? principal.Identity?.Name
            ?? throw new UnauthenticatedException("Требуется аутентификация");

        // subject = keycloak sub, либо dev:username (как Principal.subject в Python).
        var subject = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? $"dev:{username}";
        var email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value
            ?? "";
        var fullName = principal.FindFirst("name")?.Value ?? username;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        var user = await SyncAsync(subject, username, email, fullName, roles, ct);
        if (!user.IsActive)
            throw new ForbiddenException("Учётная запись деактивирована");

        _cached = user;
        return user;
    }

    private async Task<User> SyncAsync(
        string subject, string username, string email, string fullName, List<string> roles, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.KeycloakId == subject, ct)
                   ?? await db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

        if (user is null)
        {
            user = new User
            {
                KeycloakId = subject,
                Username = username,
                Email = string.IsNullOrEmpty(email) ? $"{username}@unknown.local" : email,
                FullName = string.IsNullOrEmpty(fullName) ? username : fullName,
                Roles = roles,
                IsActive = true,
            };
            db.Users.Add(user);
        }
        else
        {
            user.KeycloakId = subject;
            if (!string.IsNullOrEmpty(email)) user.Email = email;
            // Не затираем «настоящее» ФИО dev-логином, где name == username.
            if (!string.IsNullOrEmpty(fullName) && fullName != username) user.FullName = fullName;
            user.Roles = roles;
            if (!user.IsActive) user.IsActive = true;
        }

        await db.SaveChangesAsync(ct);
        return user;
    }
}
