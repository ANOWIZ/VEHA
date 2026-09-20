using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Veha.Api.Auth;
using Veha.Api.Config;
using Veha.Api.Dtos;
using Veha.Domain.Common;

namespace Veha.Api.Controllers;

/// <summary>Аутентификация: dev-login (только вне production) и текущий пользователь.
/// Порт api/v1/auth.py.</summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    CurrentUserAccessor current, IOptions<VehaSettings> settings, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("dev-login")]
    [AllowAnonymous]
    public DevLoginResponseDto DevLogin(DevLoginRequestDto body)
    {
        if (!(settings.Value.AuthDevMode && !env.IsProduction()))
            throw new ForbiddenException("Dev-аутентификация отключена (включена только вне production)");

        var roles = body.Roles.Count > 0 ? body.Roles : ["engineer"];
        var token = DevToken.Issue(body.Username, roles, body.Email, body.FullName, settings.Value.DevJwtSecret);
        return new DevLoginResponseDto(token);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<UserPublicDto> Me(CancellationToken ct)
    {
        var u = await current.GetAsync(ct);
        return new UserPublicDto(u.Id, u.Username, u.Email, u.FullName,
            u.Department, u.Position, u.Grade, u.IsActive, u.Roles.ToList());
    }
}
