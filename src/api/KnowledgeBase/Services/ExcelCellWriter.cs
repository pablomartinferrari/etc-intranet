using System.Text.Json;
using ClosedXML.Excel;

namespace Intranet.Api.KnowledgeBase.Services;

internal static class ExcelCellWriter
{
    public static string HeaderText(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "TRUE",
            JsonValueKind.False => "FALSE",
            _ => element.ToString(),
        };

    public static void Write(IXLCell cell, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString() ?? string.Empty;
                if (text.StartsWith('='))
                {
                    cell.FormulaA1 = text;
                }
                else
                {
                    cell.Value = text;
                }

                break;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out var integer))
                {
                    cell.Value = integer;
                }
                else if (value.TryGetDecimal(out var decimalValue))
                {
                    cell.Value = decimalValue;
                }
                else
                {
                    cell.Value = value.GetDouble();
                }

                break;
            case JsonValueKind.True:
                cell.Value = true;
                break;
            case JsonValueKind.False:
                cell.Value = false;
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                cell.Value = string.Empty;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }
}
