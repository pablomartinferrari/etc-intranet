using ClosedXML.Excel;
using Intranet.Api.KnowledgeBase.Models;

namespace Intranet.Api.KnowledgeBase.Services;

public static class ChatExcelBuilder
{
    private const int MaxSheets = 5;
    private const int MaxColumns = 50;
    private const int MaxRows = 500;

    public static byte[] Build(ExcelExportSpec spec)
    {
        var sheets = spec.Sheets?.Where(s => s.Headers is { Count: > 0 }).ToList() ?? [];
        if (sheets.Count == 0)
        {
            throw new InvalidOperationException("Excel export requires at least one sheet with headers.");
        }

        using var workbook = new XLWorkbook();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sheetSpec in sheets.Take(MaxSheets))
        {
            var headers = sheetSpec.Headers!.Take(MaxColumns).Select(ExcelCellWriter.HeaderText).ToList();
            var name = UniqueSheetName(SanitizeSheetName(sheetSpec.Name ?? "Sheet1"), usedNames);
            var ws = workbook.Worksheets.Add(name);

            for (var col = 0; col < headers.Count; col++)
            {
                ws.Cell(1, col + 1).Value = headers[col];
            }

            var headerRow = ws.Range(1, 1, 1, headers.Count);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

            var rows = sheetSpec.Rows ?? [];
            var rowIndex = 2;
            foreach (var row in rows.Take(MaxRows))
            {
                for (var col = 0; col < headers.Count; col++)
                {
                    var cell = ws.Cell(rowIndex, col + 1);
                    if (col < row.Count)
                    {
                        ExcelCellWriter.Write(cell, row[col]);
                    }
                }

                rowIndex++;
            }

            ws.Columns().AdjustToContents(1, Math.Min(20, headers.Count));
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        foreach (var ch in invalid)
        {
            name = name.Replace(ch, '-');
        }

        name = name.Trim();
        if (name.Length > 31)
        {
            name = name[..31];
        }

        return string.IsNullOrWhiteSpace(name) ? "Sheet1" : name;
    }

    private static string UniqueSheetName(string name, HashSet<string> used)
    {
        if (used.Add(name))
        {
            return name;
        }

        for (var i = 2; i < 100; i++)
        {
            var suffix = $" ({i})";
            var trimmed = name.Length + suffix.Length > 31 ? name[..(31 - suffix.Length)] : name;
            var candidate = trimmed + suffix;
            if (used.Add(candidate))
            {
                return candidate;
            }
        }

        return $"Sheet{used.Count + 1}";
    }
}
