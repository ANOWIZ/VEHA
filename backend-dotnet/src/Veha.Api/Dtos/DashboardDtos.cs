using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/dashboard.py.

public record PortfolioRowDto(
    Guid ProjectId, string Code, string Name, Stage Stage, ProjectStatus Status, Guid ManagerId,
    decimal Revenue, decimal Margin, decimal MarginPct,
    decimal PlannedHours, decimal ActualHours, decimal HoursOverrunPct, List<string> Risks);

public record PortfolioResponseDto(
    int ProjectsCount, decimal TotalRevenue, decimal TotalMargin, decimal AvgMarginPct,
    int AtRisk, List<PortfolioRowDto> Rows);
