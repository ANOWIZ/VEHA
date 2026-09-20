using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Калькулятор ТКП: версии, строки, статусы, экспорт XLSX. Доступ через
/// ProjectAccess; чтение — presale/pm/admin/director/finance; правка — presale/pm/admin.
/// Порт api/v1/quotes.py.</summary>
[ApiController]
[Route("api/v1/projects/{projectId:guid}/quotes")]
[Authorize]
public class QuotesController(
    CurrentUserAccessor current, ProjectAccessService access, QuoteService quotes) : ControllerBase
{
    private const string EditRoles = "presale,pm,admin";
    private const string ViewRoles = "presale,pm,admin,director,finance";

    [HttpGet]
    [Authorize(Roles = ViewRoles)]
    public async Task<List<QuoteOutDto>> List(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await quotes.ListForProjectAsync(projectId, ct);
    }

    [HttpPost]
    [Authorize(Roles = EditRoles)]
    public async Task<IActionResult> Create(Guid projectId, QuoteCreateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return StatusCode(StatusCodes.Status201Created, await quotes.CreateAsync(projectId, body, user.Id, ct));
    }

    [HttpGet("{quoteId:guid}")]
    [Authorize(Roles = ViewRoles)]
    public async Task<QuoteDetailDto> Get(Guid projectId, Guid quoteId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await quotes.GetAsync(quoteId, projectId, ct);
    }

    [HttpPost("{quoteId:guid}/lines")]
    [Authorize(Roles = EditRoles)]
    public async Task<QuoteDetailDto> AddLine(Guid projectId, Guid quoteId, QuoteLineInputDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await quotes.AddLineAsync(quoteId, body, projectId, ct);
    }

    [HttpPut("{quoteId:guid}/lines/{lineId:guid}")]
    [Authorize(Roles = EditRoles)]
    public async Task<QuoteDetailDto> UpdateLine(Guid projectId, Guid quoteId, Guid lineId, QuoteLineInputDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await quotes.UpdateLineAsync(quoteId, lineId, body, projectId, ct);
    }

    [HttpDelete("{quoteId:guid}/lines/{lineId:guid}")]
    [Authorize(Roles = EditRoles)]
    public async Task<QuoteDetailDto> DeleteLine(Guid projectId, Guid quoteId, Guid lineId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await quotes.DeleteLineAsync(quoteId, lineId, projectId, ct);
    }

    [HttpPost("{quoteId:guid}/status")]
    [Authorize(Roles = EditRoles)]
    public async Task<QuoteOutDto> SetStatus(Guid projectId, Guid quoteId, QuoteStatusUpdateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await quotes.SetStatusAsync(quoteId, body.Status, user.Id, projectId, ct);
    }

    [HttpPost("{quoteId:guid}/clone")]
    [Authorize(Roles = EditRoles)]
    public async Task<QuoteDetailDto> Clone(Guid projectId, Guid quoteId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await quotes.CloneAsync(quoteId, user.Id, projectId, ct);
    }

    [HttpGet("{quoteId:guid}/export.xlsx")]
    [Authorize(Roles = ViewRoles)]
    public async Task<IActionResult> ExportXlsx(Guid projectId, Guid quoteId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var project = await access.RequireAccessAsync(projectId, user, ct);
        var quote = await quotes.GetEntityAsync(quoteId, projectId, ct);
        var bytes = QuoteExport.Build(quote, project.Code, project.Name);
        var filename = $"TKP_{project.Code}_v{quote.Version}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }
}
