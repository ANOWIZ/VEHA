using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Veha.Api.Controllers;

/// <summary>Текущий пользователь из токена/dev-заголовка (проверка аутентификации и ролей).</summary>
[ApiController]
[Route("api/v1/me")]
public class MeController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Get() => Ok(new
    {
        username = User.Identity?.Name,
        roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
    });
}
