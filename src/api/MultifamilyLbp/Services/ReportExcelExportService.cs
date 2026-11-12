using System.Text.Json;
using ClosedXML.Excel;

namespace Intranet.Api.MultifamilyLbp.Services;

public sealed class ReportExcelExportService
{
    private static readonly Dictionary<string, string[]> FixedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["averageComponents"] = ["component", "substrate", "result", "positivePercent", "negativePercent", "totalReadings"],
        ["uniformShots"] = ["component", "substrate", "result", "totalReadings"],
        ["nonUniformShots"] = ["component", "substrate", "positiveCount", "negativeCount", "positivePercent", "totalReadings"],
    };

    private static readonly Dictionary<string, string> SheetTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["allShots"] = "All shots",
        ["uniformShots"] = "Uniform",
        ["nonUniformShots"] = "Non-uniform",
        ["averageComponents"] = "Average",
    };

    public byte[] Export(object reportResult, string jobId, string dataType, DateTimeOffset generatedAt)
    {
        var root = JsonSerializer.SerializeToElement(reportResult);
        using var workbook = new XLWorkbook();

        foreach (var (key, title) in SheetTitles)
        {
            if (!root.TryGetProperty(key, out var section) || section.ValueKind != JsonValueKind.Array)
                continue;
            if (section.GetArrayLength() == 0)
                continue;

            var ws = workbook.Worksheets.Add(SanitizeSheetName(title));
            if (string.Equals(key, "nonUniformShots", StringComparison.OrdinalIgnoreCase))
                WriteNonUniformSection(ws, section, FixedColumns[key]);
            else
                WriteSection(ws, section, FixedColumns.GetValueOrDefault(key));
        }

        if (workbook.Worksheets.Count == 0)
        {
            var ws = workbook.Worksheets.Add("Report");
            ws.Cell(1, 1).Value = "No report data";
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static readonly string[] NonUniformDetailColumns =
        ["readingId", "substrate", "location", "leadContent", "isPositive"];

    private static void WriteNonUniformSection(IXLWorksheet ws, JsonElement rows, string[] summaryColumns)
    {
        var rowIndex = WriteSection(ws, rows, summaryColumns, startRow: 1);

        foreach (var item in rows.EnumerateArray())
        {
            if (!item.TryGetProperty("readings", out var readings) || readings.ValueKind != JsonValueKind.Array)
                continue;
            if (readings.GetArrayLength() == 0)
                continue;

            var component = item.TryGetProperty("component", out var compEl) ? compEl.ToString() : "";
            rowIndex += 1;
            ws.Cell(rowIndex, 1).Value = $"{component} — individual readings";
            ws.Row(rowIndex).Style.Font.Bold = true;
            rowIndex += 1;

            for (var c = 0; c < NonUniformDetailColumns.Length; c++)
                ws.Cell(rowIndex, c + 1).Value = HeaderLabel(NonUniformDetailColumns[c]);
            ws.Row(rowIndex).Style.Font.Bold = true;
            ws.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.LightGray;
            rowIndex += 1;

            foreach (var reading in readings.EnumerateArray())
            {
                for (var c = 0; c < NonUniformDetailColumns.Length; c++)
                {
                    var col = NonUniformDetailColumns[c];
                    if (!reading.TryGetProperty(col, out var val))
                        ws.Cell(rowIndex, c + 1).Value = "";
                    else
                        WriteCell(ws.Cell(rowIndex, c + 1), col, val);
                }
                rowIndex += 1;
            }

            rowIndex += 1;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static int WriteSection(IXLWorksheet ws, JsonElement rows, string[]? columnOrder, int startRow = 1)
    {
        var first = rows[0];
        var columns = columnOrder ?? first.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        for (var c = 0; c < columns.Length; c++)
            ws.Cell(startRow, c + 1).Value = HeaderLabel(columns[c]);

        var headerRow = ws.Row(startRow);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var rowIndex = startRow + 1;
        foreach (var item in rows.EnumerateArray())
        {
            for (var c = 0; c < columns.Length; c++)
            {
                var col = columns[c];
                if (!item.TryGetProperty(col, out var val))
                {
                    ws.Cell(rowIndex, c + 1).Value = "";
                    continue;
                }

                WriteCell(ws.Cell(rowIndex, c + 1), col, val);
            }

            rowIndex++;
        }

        if (startRow == 1)
        {
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);
        }

        return rowIndex;
    }

    private static void WriteCell(IXLCell cell, string column, JsonElement val)
    {
        if (val.ValueKind == JsonValueKind.Null || val.ValueKind == JsonValueKind.Undefined)
        {
            cell.Value = "";
            return;
        }

        if (column == "result" && val.ValueKind == JsonValueKind.String)
        {
            var s = val.GetString() ?? "";
            cell.Value = s.Equals("POSITIVE", StringComparison.OrdinalIgnoreCase) ? "Positive" : "Negative";
            return;
        }

        if (column is "positivePercent" && val.ValueKind == JsonValueKind.Number)
        {
            cell.Value = val.GetDouble();
            cell.Style.NumberFormat.Format = "0.00\"%\"";
            return;
        }

        if (val.ValueKind == JsonValueKind.Number)
        {
            cell.Value = val.GetDouble();
            return;
        }

        if (val.ValueKind == JsonValueKind.True || val.ValueKind == JsonValueKind.False)
        {
            cell.Value = val.GetBoolean() ? "Yes" : "No";
            return;
        }

        cell.Value = val.ToString();
    }

    private static string HeaderLabel(string key) => key switch
    {
        "readingId" => "Reading",
        "component" => "Component",
        "substrate" => "Substrate",
        "location" => "Location",
        "leadContent" => "Pb (mg/cm²)",
        "isPositive" => "Positive",
        "totalReadings" => "Total Readings",
        "positiveCount" => "Positive Count",
        "negativeCount" => "Negative Count",
        "positivePercent" => "Positive %",
        "negativePercent" => "Negative %",
        "result" => "Result",
        "validationStatus" => "Validation",
        "count" => "Count",
        _ => key,
    };

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var s = name;
        foreach (var c in invalid)
            s = s.Replace(c.ToString(), "");
        return s.Length > 31 ? s[..31] : s;
    }
}
