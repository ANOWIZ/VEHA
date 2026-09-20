using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/report.py.

public record UtilizationRowDto(
    Guid UserId, string FullName, string? Department,
    decimal BillableHours, decimal CapacityHours, decimal UtilizationPct, int ProjectsCount);

public record UtilizationReportDto(
    DateOnly DateFrom, DateOnly DateTo, int WorkingDays, decimal CapacityPerUser, int UsersCount,
    decimal TotalBillableHours, decimal TotalCapacityHours, decimal AvgUtilizationPct, List<UtilizationRowDto> Rows);

/// <summary>Строка выгрузки трудозатрат (для XLSX; не JSON-ответ).</summary>
public record TimesheetExportRow(
    DateOnly WorkDate, string User, string ProjectCode, string ProjectName, string? Task,
    decimal Hours, TimeEntryStatus Status, string Comment, decimal? Cost);
