using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Enums;

namespace Veha.Api.Controllers;

/// <summary>Отчёты: загрузка ресурсов и XLSX-выгрузки (таймшиты, портфель). Порт reports.py.
/// utilization — только руководству; таймшиты/портфель — управленческим ролям.</summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController(
    CurrentUserAccessor current, ReportService reports, DashboardService dashboards) : ControllerBase
{
    private const string MgmtRoles = "director,admin,finance";
    private const string ReportRoles = "pm,director,admin,finance";
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int MaxRangeDays = 366;

    private static void AssertRange(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new DomainValidationException("Дата окончания периода не может быть раньше даты начала");
        if (to.DayNumber - from.DayNumber > MaxRangeDays)
            throw new DomainValidationException($"Период отчёта не должен превышать {MaxRangeDays} дней");
    }

    [HttpGet("utilization")]
    [Authorize(Roles = MgmtRoles)]
    public async Task<UtilizationReportDto> Utilization(
        [FromQuery(Name = "date_from")] DateOnly dateFrom,
        [FromQuery(Name = "date_to")] DateOnly dateTo,
        CancellationToken ct)
    {
        AssertRange(dateFrom, dateTo);
        return await reports.UtilizationAsync(dateFrom, dateTo, ct);
    }

    [HttpGet("utilization/export.xlsx")]
    [Authorize(Roles = MgmtRoles)]
    public async Task<IActionResult> UtilizationXlsx(
        [FromQuery(Name = "date_from")] DateOnly dateFrom,
        [FromQuery(Name = "date_to")] DateOnly dateTo,
        CancellationToken ct)
    {
        AssertRange(dateFrom, dateTo);
        var data = await reports.UtilizationAsync(dateFrom, dateTo, ct);
        return File(ReportExport.Utilization(data), XlsxType, $"utilization_{dateFrom}_{dateTo}.xlsx");
    }

    [HttpGet("timesheets/export.xlsx")]
    [Authorize(Roles = ReportRoles)]
    public async Task<IActionResult> TimesheetsXlsx(
        [FromQuery(Name = "date_from")] DateOnly dateFrom,
        [FromQuery(Name = "date_to")] DateOnly dateTo,
        [FromQuery(Name = "project_id")] Guid? projectId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        AssertRange(dateFrom, dateTo);
        var user = await current.GetAsync(ct);
        var includeCost = ReportService.CanSeeFinancials(user);
        var rows = await reports.TimesheetRowsAsync(
            user, dateFrom, dateTo, projectId, EnumQuery.Parse<TimeEntryStatus>(status), includeCost, ct);
        return File(ReportExport.Timesheets(rows, includeCost, dateFrom, dateTo), XlsxType,
            $"timesheets_{dateFrom}_{dateTo}.xlsx");
    }

    [HttpGet("portfolio/export.xlsx")]
    [Authorize(Roles = ReportRoles)]
    public async Task<IActionResult> PortfolioXlsx(CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var portfolio = await dashboards.PortfolioAsync(user, ct);
        var names = await reports.AllUserNamesAsync(ct);
        return File(ReportExport.Portfolio(portfolio, names), XlsxType, "portfolio.xlsx");
    }
}
