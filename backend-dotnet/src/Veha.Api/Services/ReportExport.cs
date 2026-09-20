using ClosedXML.Excel;
using Veha.Api.Dtos;
using Veha.Domain.Enums;

namespace Veha.Api.Services;

/// <summary>Серверная генерация XLSX-отчётов (utilization/таймшиты/портфель) на ClosedXML.
/// Порт report_export.py.</summary>
public static class ReportExport
{
    private const string MoneyFmt = "# ##0.00\\ ₽";
    private const string HoursFmt = "# ##0.00";
    private const string PctFmt = "0.0";

    private static readonly Dictionary<string, string> StageLabels = new()
    {
        ["presale"] = "Пресейл", ["survey"] = "Обследование", ["design"] = "Проектирование",
        ["implementation"] = "Внедрение", ["pilot"] = "Опытная эксплуатация",
        ["support"] = "Поддержка", ["closed"] = "Закрыт",
    };
    private static readonly Dictionary<string, string> TsStatusLabels = new()
    {
        ["draft"] = "Черновик", ["submitted"] = "На утверждении", ["approved"] = "Утверждён", ["rejected"] = "Отклонён",
    };
    private static readonly Dictionary<string, string> RiskLabels = new()
    {
        ["low_margin"] = "Низкая маржа", ["hours_overrun"] = "Перерасход часов",
    };

    private static void Header(IXLWorksheet ws, string[] headers, int row)
    {
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        }
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] Utilization(UtilizationReportDto r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Загрузка ресурсов");
        ws.Cell(1, 1).Value = "Отчёт по загрузке ресурсов (utilization)";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"Период: {r.DateFrom} — {r.DateTo}  ·  рабочих дней: {r.WorkingDays}  ·  ёмкость на сотрудника: {r.CapacityPerUser} ч";

        Header(ws, ["Сотрудник", "Подразделение", "Билируемые часы", "Ёмкость, ч", "Загрузка, %", "Проектов"], 4);
        var row = 5;
        foreach (var u in r.Rows)
        {
            ws.Cell(row, 1).Value = XlsxSafe.Safe(u.FullName);
            ws.Cell(row, 2).Value = XlsxSafe.Safe(u.Department ?? "—");
            ws.Cell(row, 3).Value = (double)u.BillableHours;
            ws.Cell(row, 4).Value = (double)u.CapacityHours;
            ws.Cell(row, 5).Value = (double)u.UtilizationPct;
            ws.Cell(row, 6).Value = u.ProjectsCount;
            ws.Cell(row, 3).Style.NumberFormat.Format = HoursFmt;
            ws.Cell(row, 4).Style.NumberFormat.Format = HoursFmt;
            ws.Cell(row, 5).Style.NumberFormat.Format = PctFmt;
            row++;
        }
        row++;
        ws.Cell(row, 1).Value = "ИТОГО:";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = (double)r.TotalBillableHours;
        ws.Cell(row, 4).Value = (double)r.TotalCapacityHours;
        ws.Cell(row, 5).Value = (double)r.AvgUtilizationPct;
        return Save(wb);
    }

    public static byte[] Timesheets(List<TimesheetExportRow> rows, bool includeCost, DateOnly from, DateOnly to)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Трудозатраты");
        ws.Cell(1, 1).Value = "Выгрузка трудозатрат";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"Период: {from} — {to}";

        var headers = new List<string> { "Дата", "Сотрудник", "Код", "Проект", "Задача", "Часы", "Статус", "Комментарий" };
        if (includeCost) headers.Add("Себестоимость");
        Header(ws, headers.ToArray(), 4);

        var row = 5;
        decimal totalHours = 0, totalCost = 0;
        foreach (var r in rows)
        {
            totalHours += r.Hours;
            ws.Cell(row, 1).Value = r.WorkDate.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd";
            ws.Cell(row, 2).Value = XlsxSafe.Safe(r.User);
            ws.Cell(row, 3).Value = XlsxSafe.Safe(r.ProjectCode);
            ws.Cell(row, 4).Value = XlsxSafe.Safe(r.ProjectName);
            ws.Cell(row, 5).Value = XlsxSafe.Safe(r.Task ?? "—");
            ws.Cell(row, 6).Value = (double)r.Hours;
            ws.Cell(row, 6).Style.NumberFormat.Format = HoursFmt;
            ws.Cell(row, 7).Value = TsStatusLabels.GetValueOrDefault(r.Status.ToString().ToLowerInvariant(), r.Status.ToString());
            ws.Cell(row, 8).Value = XlsxSafe.Safe(r.Comment);
            if (includeCost)
            {
                totalCost += r.Cost ?? 0m;
                ws.Cell(row, 9).Value = (double)(r.Cost ?? 0m);
                ws.Cell(row, 9).Style.NumberFormat.Format = MoneyFmt;
            }
            row++;
        }
        row++;
        ws.Cell(row, 5).Value = "ИТОГО:";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = (double)totalHours;
        ws.Cell(row, 6).Style.NumberFormat.Format = HoursFmt;
        if (includeCost)
        {
            ws.Cell(row, 9).Value = (double)totalCost;
            ws.Cell(row, 9).Style.NumberFormat.Format = MoneyFmt;
        }
        return Save(wb);
    }

    public static byte[] Portfolio(PortfolioResponseDto portfolio, IReadOnlyDictionary<Guid, string> managerNames)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Портфель");
        ws.Cell(1, 1).Value = "Портфель проектов: выручка, маржа, риски";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"Проектов: {portfolio.ProjectsCount}  ·  выручка: {portfolio.TotalRevenue}  ·  маржа: {portfolio.TotalMargin} ({portfolio.AvgMarginPct}%)  ·  в зоне риска: {portfolio.AtRisk}";

        Header(ws, ["Код", "Проект", "Стадия", "РП", "Выручка", "Маржа", "Маржа, %", "Часы факт", "Часы план", "Перерасход, %", "Риски"], 4);
        var row = 5;
        foreach (var r in portfolio.Rows)
        {
            var risks = r.Risks.Count > 0
                ? string.Join(", ", r.Risks.Select(x => RiskLabels.GetValueOrDefault(x, x)))
                : "—";
            ws.Cell(row, 1).Value = XlsxSafe.Safe(r.Code);
            ws.Cell(row, 2).Value = XlsxSafe.Safe(r.Name);
            ws.Cell(row, 3).Value = StageLabels.GetValueOrDefault(r.Stage.ToString().ToLowerInvariant(), r.Stage.ToString());
            ws.Cell(row, 4).Value = XlsxSafe.Safe(managerNames.GetValueOrDefault(r.ManagerId, "—"));
            ws.Cell(row, 5).Value = (double)r.Revenue;
            ws.Cell(row, 6).Value = (double)r.Margin;
            ws.Cell(row, 7).Value = (double)r.MarginPct;
            ws.Cell(row, 8).Value = (double)r.ActualHours;
            ws.Cell(row, 9).Value = (double)r.PlannedHours;
            ws.Cell(row, 10).Value = (double)r.HoursOverrunPct;
            ws.Cell(row, 11).Value = XlsxSafe.Safe(risks);
            ws.Cell(row, 5).Style.NumberFormat.Format = MoneyFmt;
            ws.Cell(row, 6).Style.NumberFormat.Format = MoneyFmt;
            ws.Cell(row, 7).Style.NumberFormat.Format = PctFmt;
            ws.Cell(row, 8).Style.NumberFormat.Format = HoursFmt;
            ws.Cell(row, 9).Style.NumberFormat.Format = HoursFmt;
            ws.Cell(row, 10).Style.NumberFormat.Format = PctFmt;
            row++;
        }
        return Save(wb);
    }
}
