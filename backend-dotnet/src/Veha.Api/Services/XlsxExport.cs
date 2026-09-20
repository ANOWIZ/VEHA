using ClosedXML.Excel;
using Veha.Domain.Entities;
using Veha.Domain.Enums;

namespace Veha.Api.Services;

/// <summary>Защита от формульной инъекции в XLSX (порт xlsx_safe.safe_text):
/// строки, начинающиеся с = + - @ или управляющих, префиксуются апострофом.</summary>
public static class XlsxSafe
{
    public static string Safe(string? text)
    {
        var s = text ?? "";
        return s.Length > 0 && "=+-@\t\r".IndexOf(s[0]) >= 0 ? "'" + s : s;
    }
}

/// <summary>Серверная генерация XLSX-спецификации ТКП (порт quote_export.py на ClosedXML).</summary>
public static class QuoteExport
{
    private const string MoneyFmt = "# ##0.00\\ ₽";

    private static readonly Dictionary<QuoteLineKind, string> KindLabels = new()
    {
        [QuoteLineKind.License] = "Лицензия", [QuoteLineKind.Work] = "Работы",
        [QuoteLineKind.Subcontract] = "Субподряд", [QuoteLineKind.Support] = "Техподдержка",
    };

    private static readonly Dictionary<LicensingModel, string> ModelLabels = new()
    {
        [LicensingModel.PerUser] = "за пользователя", [LicensingModel.PerNamedUser] = "именованный пользователь",
        [LicensingModel.PerConcurrentUser] = "конкурентный пользователь", [LicensingModel.PerCore] = "за ядро",
        [LicensingModel.PerCpu] = "за CPU", [LicensingModel.PerServer] = "за сервер",
        [LicensingModel.PerDevice] = "за устройство", [LicensingModel.Subscription] = "подписка",
        [LicensingModel.Perpetual] = "бессрочно",
    };

    public static byte[] Build(Quote quote, string projectCode, string projectName)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add($"ТКП v{quote.Version}");

        ws.Cell(1, 1).Value = XlsxSafe.Safe($"{quote.Title} — {projectCode}");
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = XlsxSafe.Safe(projectName);
        ws.Cell(3, 1).Value = $"Версия: {quote.Version}    Буфер курса: {quote.CurrencyBufferPct}%";
        ws.Cell(3, 1).Style.Font.Italic = true;

        string[] headers =
        [
            "Тип", "Наименование", "Модель/метрика", "Кол-во", "Цена за ед.", "Валюта",
            "Скидка партн., %", "Скидка клиенту, %", "Себестоимость", "Цена продажи", "Маржа",
        ];
        const int headerRow = 5;
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            cell.Style.Alignment.WrapText = true;
        }

        var row = headerRow + 1;
        foreach (var line in quote.Lines.OrderBy(l => l.Kind))
        {
            var modelMetric = "";
            if (line.LicensingModel is not null)
                modelMetric = ModelLabels.GetValueOrDefault(line.LicensingModel.Value, line.LicensingModel.Value.ToString());
            if (!string.IsNullOrEmpty(line.Metric))
                modelMetric = string.IsNullOrEmpty(modelMetric) ? line.Metric : $"{modelMetric} / {line.Metric}";

            ws.Cell(row, 1).Value = KindLabels.GetValueOrDefault(line.Kind, line.Kind.ToString());
            ws.Cell(row, 2).Value = XlsxSafe.Safe(line.Name);
            ws.Cell(row, 3).Value = XlsxSafe.Safe(modelMetric);
            ws.Cell(row, 4).Value = (double)line.Qty;
            ws.Cell(row, 5).Value = (double)line.UnitPrice;
            ws.Cell(row, 6).Value = line.Currency;
            ws.Cell(row, 7).Value = (double)line.PartnerDiscountPct;
            ws.Cell(row, 8).Value = (double)line.ClientDiscountPct;
            ws.Cell(row, 9).Value = (double)line.CostAmount;
            ws.Cell(row, 10).Value = (double)line.SellAmount;
            ws.Cell(row, 11).Value = (double)line.Margin;
            foreach (var mc in new[] { 5, 9, 10, 11 })
                ws.Cell(row, mc).Style.NumberFormat.Format = MoneyFmt;
            row++;
        }

        row += 1;
        ws.Cell(row, 8).Value = "ИТОГО:";
        ws.Cell(row, 8).Style.Font.Bold = true;
        foreach (var (col, key) in new[] { (9, "total_cost"), (10, "total_sell"), (11, "margin") })
        {
            ws.Cell(row, col).Value = ParseTotal(quote.Totals, key);
            ws.Cell(row, col).Style.Font.Bold = true;
            ws.Cell(row, col).Style.NumberFormat.Format = MoneyFmt;
        }
        ws.Cell(row + 1, 10).Value = $"Маржинальность: {quote.Totals.GetValueOrDefault("margin_pct", "0")}%";
        ws.Cell(row + 1, 10).Style.Font.Bold = true;

        double[] widths = [14, 36, 22, 9, 14, 8, 14, 14, 16, 16, 16];
        for (var i = 0; i < widths.Length; i++)
            ws.Column(i + 1).Width = widths[i];

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static double ParseTotal(IReadOnlyDictionary<string, string> totals, string key)
        => double.TryParse(totals.GetValueOrDefault(key, "0"),
            System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
}
