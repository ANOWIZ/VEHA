using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Справочник клиентов (заказчиков). Чтение — любой авторизованный;
/// создание/изменение/удаление — admin/pm/presale/director. Порт api/v1/clients.py.</summary>
[ApiController]
[Route("api/v1/clients")]
[Authorize]
public class ClientsController(ClientService clients) : ControllerBase
{
    // Управление справочником — pm/presale/admin/director (как _manage в Python-роутере).
    private const string ManageRoles = "admin,pm,presale,director";

    [HttpGet]
    public Task<Page<ClientDto>> List(
        [FromQuery] string? q,
        [FromQuery, Range(1, 500)] int limit = 50,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        CancellationToken ct = default)
        => clients.SearchAsync(q, limit, offset, ct);

    [HttpGet("{clientId:guid}")]
    public Task<ClientDto> Get(Guid clientId, CancellationToken ct)
        => clients.GetAsync(clientId, ct);

    [HttpPost]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> Create(ClientCreateDto body, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await clients.CreateAsync(body, ct));

    [HttpPatch("{clientId:guid}")]
    [Authorize(Roles = ManageRoles)]
    public Task<ClientDto> Update(Guid clientId, ClientUpdateDto body, CancellationToken ct)
        => clients.UpdateAsync(clientId, body, ct);

    [HttpDelete("{clientId:guid}")]
    [Authorize(Roles = ManageRoles)]
    public async Task<MessageDto> Delete(Guid clientId, CancellationToken ct)
    {
        await clients.DeleteAsync(clientId, ct);
        return new MessageDto("Клиент удалён");
    }
}
