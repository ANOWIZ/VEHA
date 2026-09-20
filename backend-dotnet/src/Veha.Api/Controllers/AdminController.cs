using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Администрирование: аудит-лог и выгрузка в 1С. Порт api/v1/admin.py.</summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize]
public class AdminController(AuditService audit, OneCService onec) : ControllerBase
{
    [HttpGet("audit")]
    [Authorize(Roles = "admin,director,finance")]
    public Task<Page<AuditEntryOutDto>> Audit(
        [FromQuery] string? entity,
        [FromQuery(Name = "entity_id")] Guid? entityId,
        [FromQuery, Range(1, 500)] int limit = 100,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        CancellationToken ct = default)
        => audit.ListAsync(entity, entityId, limit, offset, ct);

    [HttpPost("integrations/1c/export-timesheets")]
    [Authorize(Roles = "admin,finance")]
    public Task<object> Export1C(
        [FromQuery(Name = "period_from")] DateOnly periodFrom,
        [FromQuery(Name = "period_to")] DateOnly periodTo,
        CancellationToken ct = default)
        => onec.ExportTimesheetsAsync(periodFrom, periodTo, ct);
}
