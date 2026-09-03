using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;
using UglyToad.PdfPig;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public static class DocumentTextExtractor
{
    public static string? Extract(string fileName, byte[] bytes)
    {
        var ext = AgentSourceFileRules.GetExtension(fileName);
        try
        {
            return ext switch
            {
                ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".htm" or ".rtf" => ReadText(bytes),
                ".pdf" => ExtractPdf(bytes),
                ".docx" or ".odt" => ExtractOpenXml(bytes, "word/document.xml", "content.xml"),
                ".pptx" or ".odp" => ExtractPptx(bytes),
                ".xlsx" or ".ods" => ExtractXlsx(bytes),
                _ => null,
            };
        }
        catch (Exception)
        {
            return null;
        }
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

    private static string? ExtractPdf(byte[] bytes)
    {
        using var doc = PdfDocument.Open(bytes);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    private static string? ExtractOpenXml(byte[] bytes, params string[] entryNames)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, leaveOpen: false);
        foreach (var name in entryNames)
        {
            var entry = zip.GetEntry(name);
            if (entry is null)
            {
                continue;
            }

            using var stream = entry.Open();
            var xml = XDocument.Load(stream);
            var texts = xml.Descendants().Where(e => e.Name.LocalName is "t" or "a" or "p");
            return string.Join('\n', texts.Select(t => t.Value).Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        return null;
    }

    private static string? ExtractPptx(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, leaveOpen: false);
        var sb = new StringBuilder();
        foreach (var entry in zip.Entries.Where(e =>
                     e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase)
                     && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
        {
            using var stream = entry.Open();
            var xml = XDocument.Load(stream);
            foreach (var node in xml.Descendants().Where(e => e.Name.LocalName == "t"))
            {
                if (!string.IsNullOrWhiteSpace(node.Value))
                {
                    sb.AppendLine(node.Value);
                }
            }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    private static string? ExtractXlsx(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var sb = new StringBuilder();
        foreach (var sheet in workbook.Worksheets)
        {
            sb.AppendLine(sheet.Name);
            foreach (var row in sheet.RowsUsed())
            {
                var cells = row.CellsUsed().Select(c => c.GetString()).Where(v => !string.IsNullOrWhiteSpace(v));
                var line = string.Join('\t', cells);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine(line);
                }
            }
        }

        return sb.ToString();
    }
}

public static class TextChunker
{
    public const int DefaultChunkChars = 3000;
    public const int DefaultOverlapChars = 200;

    public static IReadOnlyList<string> Chunk(string text, int chunkChars = DefaultChunkChars, int overlapChars = DefaultOverlapChars)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = CollapseWhitespace(text);
        if (normalized.Length <= chunkChars)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var start = 0;
        while (start < normalized.Length)
        {
            var length = Math.Min(chunkChars, normalized.Length - start);
            var end = start + length;
            if (end < normalized.Length)
            {
                var breakAt = normalized.LastIndexOfAny([' ', '\n', '.', ';'], end - 1, Math.Min(400, length));
                if (breakAt > start + chunkChars / 2)
                {
                    end = breakAt + 1;
                }
            }

            var slice = normalized[start..end].Trim();
            if (slice.Length > 0)
            {
                chunks.Add(slice);
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(end - overlapChars, start + 1);
        }

        return chunks;
    }

    private static string CollapseWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var previousWs = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWs)
                {
                    sb.Append(ch == '\n' ? '\n' : ' ');
                    previousWs = true;
                }

                continue;
            }

            previousWs = false;
            sb.Append(ch);
        }

        return sb.ToString().Trim();
    }
}
