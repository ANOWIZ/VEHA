using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Справочник пользователей и версии ставок себестоимости. Список/деталь —
/// любой авторизованный; ставка current_cost_rate — только финролям; назначение
/// ставки — admin/finance. Порт api/v1/users.py.</summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public Task<Page<UserPublicDto>> List(
        [FromQuery, Range(1, 500)] int limit = 50,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery(Name = "active_only")] bool activeOnly = true,
        CancellationToken ct = default)
        => users.ListAsync(limit, offset, activeOnly, ct);

    [HttpGet("{userId:guid}")]
    public Task<UserWithRateDto> Get(Guid userId, CancellationToken ct)
        => users.GetAsync(userId, AuthZ.CanSeeFinancials(User), ct);

    [HttpPost("{userId:guid}/cost-rates")]
    [Authorize(Roles = "admin,finance")]
    public Task<CostRateOutDto> SetCostRate(Guid userId, CostRateCreateDto body, CancellationToken ct)
        => users.SetCostRateAsync(userId, body, ct);
}
