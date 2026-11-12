using Intranet.Api.KnowledgeBase.Models;

namespace Intranet.Api.KnowledgeBase.Services;

public static class ChatExportIntent
{
    public static ExportFormat? Detect(string query)
    {
        var q = query.ToLowerInvariant();

        if (ContainsAny(q, "excel", "xlsx", "spreadsheet", ".xls"))
        {
            return ExportFormat.Excel;
        }

        if (ContainsAny(q, "word", "docx", ".doc"))
        {
            return ExportFormat.Word;
        }

        var wantsFile = ContainsAny(
            q,
            "export to",
            "export as",
            "download as",
            "create a file",
            "generate a file",
            "save as",
            "make a file",
            "write a file");

        if (!wantsFile)
        {
            return null;
        }

        if (ContainsAny(q, "table", "spreadsheet", "rows", "columns", "csv"))
        {
            return ExportFormat.Excel;
        }

        if (ContainsAny(q, "report", "memo", "letter", "summary document", "write-up"))
        {
            return ExportFormat.Word;
        }

        return null;
    }

    public static string FormatLabel(ExportFormat format) =>
        format switch
        {
            ExportFormat.Excel => "Excel (.xlsx)",
            ExportFormat.Word => "Word (.docx)",
            _ => "file",
        };

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(n => text.Contains(n, StringComparison.Ordinal));
}
