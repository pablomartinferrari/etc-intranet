using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Intranet.Api.KnowledgeBase.Models;

namespace Intranet.Api.KnowledgeBase.Services;

public static class ChatWordBuilder
{
    private const int MaxSections = 30;
    private const int MaxParagraphsPerSection = 50;

    public static byte[] Build(WordExportSpec spec)
    {
        var sections = spec.Sections?.Where(s =>
            !string.IsNullOrWhiteSpace(s.Heading) ||
            s.Paragraphs is { Count: > 0 }).ToList() ?? [];

        if (sections.Count == 0 && string.IsNullOrWhiteSpace(spec.Title))
        {
            throw new InvalidOperationException("Word export requires a title or at least one section.");
        }

        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            if (!string.IsNullOrWhiteSpace(spec.Title))
            {
                body.Append(CreateParagraph(spec.Title.Trim(), bold: true, fontSizeHalfPoints: 32));
                body.Append(CreateParagraph(string.Empty));
            }

            foreach (var section in sections.Take(MaxSections))
            {
                if (!string.IsNullOrWhiteSpace(section.Heading))
                {
                    body.Append(CreateParagraph(section.Heading.Trim(), bold: true, fontSizeHalfPoints: 28));
                }

                foreach (var paragraph in section.Paragraphs?.Take(MaxParagraphsPerSection) ?? [])
                {
                    if (string.IsNullOrWhiteSpace(paragraph))
                    {
                        continue;
                    }

                    body.Append(CreateParagraph(paragraph.Trim()));
                }

                body.Append(CreateParagraph(string.Empty));
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph CreateParagraph(string text, bool bold = false, int fontSizeHalfPoints = 24)
    {
        var runProps = new RunProperties();
        if (bold)
        {
            runProps.Append(new Bold());
        }

        runProps.Append(new FontSize { Val = fontSizeHalfPoints.ToString() });

        return new Paragraph(
            new Run(
                runProps,
                new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }
}
