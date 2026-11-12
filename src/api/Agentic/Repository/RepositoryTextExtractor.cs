using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Drawing = DocumentFormat.OpenXml.Drawing;
using Spreadsheet = DocumentFormat.OpenXml.Spreadsheet;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Intranet.Api.Agentic.Repository;

public static class RepositoryExtractionStatus
{
    public const string Ok = "ok";
    public const string Empty = "empty";
    public const string Unsupported = "unsupported";
    public const string Error = "error";
    public const string Pending = "pending";
}

public sealed record RepositoryExtraction(string Text, string Status, string? Note);

/// <summary>
/// Workspace repository text extraction. PDFs use PdfPig (content-order, then
/// page text, then words). Office files use the Open XML SDK. Azure Document
/// Intelligence is not configured in this app, so image-only / scanned files
/// are marked empty rather than OCR'd.
/// </summary>
public static class RepositoryTextExtractor
{
    public static string DetectFileType(string fileName, string? mime)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".xls" or ".xlsx" or ".xlsm" or ".csv" or ".ods")
        {
            return "spreadsheet";
        }

        if (ext is ".ppt" or ".pptx" or ".pptm" or ".odp")
        {
            return "presentation";
        }

        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" || (mime?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return "image";
        }

        return "document";
    }

    public static RepositoryExtraction Extract(string fileName, string? mime, Stream content)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        return Extract(fileName, mime, buffer.ToArray());
    }

    public static RepositoryExtraction Extract(string fileName, string? mime, byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return new RepositoryExtraction("", RepositoryExtractionStatus.Empty, "The uploaded file is empty.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mimeType = mime ?? "";
        try
        {
            if (ext is ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".htm" or ".rtf"
                || mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return Finish(ReadText(bytes), fileName, "plain text");
            }

            if (ext == ".pdf" || mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Finish(ExtractPdf(bytes), fileName, "PDF");
            }

            if (ext is ".docx" or ".docm")
            {
                return Finish(ExtractDocx(bytes), fileName, "Word");
            }

            if (ext is ".pptx" or ".pptm")
            {
                return Finish(ExtractPptx(bytes), fileName, "PowerPoint");
            }

            if (ext is ".xlsx" or ".xlsm")
            {
                return Finish(ExtractXlsx(bytes), fileName, "Excel");
            }

            if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp"
                || mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return new RepositoryExtraction(
                    "",
                    RepositoryExtractionStatus.Unsupported,
                    "Image files have no extractable text. Azure Document Intelligence is not configured, so this file was not OCR'd.");
            }

            return new RepositoryExtraction(
                "",
                RepositoryExtractionStatus.Unsupported,
                $"No text extractor is registered for {ext} files.");
        }
        catch (Exception ex)
        {
            return new RepositoryExtraction(
                "",
                RepositoryExtractionStatus.Error,
                $"Could not extract text ({ex.GetType().Name}).");
        }
    }

    public static bool IsMetadataOnly(string? text, string fileName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        var name = Path.GetFileName(fileName) ?? "";
        var stem = Path.GetFileNameWithoutExtension(fileName) ?? "";
        return normalized.Equals(name, StringComparison.OrdinalIgnoreCase)
            || (stem.Length > 0 && normalized.Equals(stem, StringComparison.OrdinalIgnoreCase));
    }

    public static bool NeedsStartupReextract(string? extractionStatus) =>
        string.IsNullOrWhiteSpace(extractionStatus)
        || extractionStatus.Equals(RepositoryExtractionStatus.Pending, StringComparison.OrdinalIgnoreCase);

    private static RepositoryExtraction Finish(string? text, string fileName, string kind)
    {
        var normalized = text ?? "";
        if (IsMetadataOnly(normalized, fileName))
        {
            return new RepositoryExtraction(
                "",
                RepositoryExtractionStatus.Empty,
                $"No extractable text in this {kind}. It may be a scan or image-only file. Try Re-index, or upload a text/DOCX copy.");
        }

        return new RepositoryExtraction(normalized, RepositoryExtractionStatus.Ok, null);
    }

    private static string ReadText(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (text.Contains('\0', StringComparison.Ordinal))
        {
            return Encoding.Latin1.GetString(bytes);
        }

        return text;
    }

    private static string ExtractPdf(byte[] bytes)
    {
        using var doc = PdfDocument.Open(bytes);
        var builder = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            var pageText = BestPageText(page);
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                builder.AppendLine(pageText);
            }
        }

        return builder.ToString();
    }

    private static string BestPageText(UglyToad.PdfPig.Content.Page page)
    {
        var ordered = Try(() => ContentOrderTextExtractor.GetText(page));
        var raw = Try(() => page.Text);
        var words = Try(() => string.Join(' ', page.GetWords().Select(w => w.Text)));
        return new[] { ordered, raw, words }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .OrderByDescending(s => s!.Length)
            .FirstOrDefault() ?? "";
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return "";
        }

        var sb = new StringBuilder();
        foreach (var paragraph in body.Elements<Word.Paragraph>())
        {
            var line = string.Concat(paragraph.Descendants<Word.Text>().Select(t => t.Text));
            if (!string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine(line);
            }
        }

        return sb.ToString();
    }

    private static string ExtractPptx(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = PresentationDocument.Open(ms, false);
        var presentation = doc.PresentationPart;
        if (presentation is null)
        {
            return "";
        }

        var sb = new StringBuilder();
        foreach (var slidePart in presentation.SlideParts)
        {
            foreach (var text in slidePart.Slide.Descendants<Drawing.Text>())
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                {
                    sb.AppendLine(text.Text);
                }
            }
        }

        return sb.ToString();
    }

    private static string ExtractXlsx(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var workbook = doc.WorkbookPart;
        if (workbook?.Workbook.Sheets is null)
        {
            return "";
        }

        var sst = workbook.SharedStringTablePart?.SharedStringTable;
        var sb = new StringBuilder();
        foreach (var sheet in workbook.Workbook.Sheets.Elements<Spreadsheet.Sheet>())
        {
            if (string.IsNullOrWhiteSpace(sheet.Id?.Value))
            {
                continue;
            }

            if (workbook.GetPartById(sheet.Id.Value) is not WorksheetPart wsPart)
            {
                continue;
            }

            sb.AppendLine(sheet.Name?.Value ?? "Sheet");
            foreach (var row in wsPart.Worksheet.Descendants<Spreadsheet.Row>())
            {
                var cells = row.Elements<Spreadsheet.Cell>()
                    .Select(c => CellText(c, sst))
                    .Where(v => !string.IsNullOrWhiteSpace(v));
                var line = string.Join('\t', cells);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine(line);
                }
            }
        }

        return sb.ToString();
    }

    private static string CellText(Spreadsheet.Cell cell, Spreadsheet.SharedStringTable? sst)
    {
        var value = cell.CellValue?.InnerText ?? "";
        if (cell.DataType?.Value == Spreadsheet.CellValues.SharedString
            && int.TryParse(value, out var index)
            && sst is not null
            && index >= 0
            && index < sst.Count())
        {
            return sst.ElementAt(index).InnerText;
        }

        if (cell.DataType?.Value == Spreadsheet.CellValues.InlineString)
        {
            return cell.InlineString?.Text?.Text ?? value;
        }

        return value;
    }

    private static string? Try(Func<string?> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return null;
        }
    }
}
