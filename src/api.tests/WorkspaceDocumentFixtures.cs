using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Intranet.Api.Tests;

internal static class WorkspaceDocumentFixtures
{
    public const string PhoneNumber = "555-0100";

    public static byte[] PdfWithPhone(string name = "Franco Ferrari", string phone = PhoneNumber)
    {
        var text = $"{name} Resume Phone {phone}";
        return MinimalPdf(text);
    }

    public static byte[] DocxWithPhone(string name = "Franco Ferrari", string phone = PhoneNumber)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(
                new Paragraph(new Run(new Text(name))),
                new Paragraph(new Run(new Text($"Phone {phone}")))));
            main.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] DocxWithTableSection(string heading = "Scope", string cellText = "Table section: phase two budget")
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            var table = new Table(
                new TableRow(
                    new TableCell(
                        new Paragraph(new Run(new Text(cellText))))));
            main.Document = new Document(new Body(
                new Paragraph(new Run(new Text(heading))),
                table,
                new Paragraph(new Run(new Text("Closing paragraph")))));
            main.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] XlsxWithPhone(string phone = PhoneNumber)
    {
        using var ms = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Contacts");
            sheet.Cell(1, 1).Value = "Name";
            sheet.Cell(1, 2).Value = "Phone";
            sheet.Cell(2, 1).Value = "Franco Ferrari";
            sheet.Cell(2, 2).Value = phone;
            workbook.SaveAs(ms);
        }

        return ms.ToArray();
    }

    /// <summary>Minimal PDF-1.4 with a single Helvetica text run.</summary>
    public static byte[] MinimalPdf(string text)
    {
        var escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
        var content = $"BT /F1 12 Tf 72 720 Td ({escaped}) Tj ET";
        var objects = new[]
        {
            "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n",
            "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n",
            "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>endobj\n",
            $"4 0 obj<< /Length {Encoding.ASCII.GetByteCount(content)} >>stream\n{content}\nendstream\nendobj\n",
            "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n",
        };

        var header = "%PDF-1.4\n";
        var offsets = new int[objects.Length];
        var position = Encoding.ASCII.GetByteCount(header);
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = position;
            position += Encoding.ASCII.GetByteCount(objects[i]);
        }

        var xref = new StringBuilder();
        xref.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            xref.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        var startxref = position;
        var trailer = $"trailer<< /Size 6 /Root 1 0 R >>\nstartxref\n{startxref}\n%%EOF\n";
        var pdf = header + string.Concat(objects) + xref + trailer;
        return Encoding.ASCII.GetBytes(pdf);
    }
}
