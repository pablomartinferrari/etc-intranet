using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intranet.Api.KnowledgeBase.Models;

public sealed class ExcelExportSpec
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("sheets")]
    public List<ExcelSheetSpec>? Sheets { get; set; }
}

public sealed class ExcelSheetSpec
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("headers")]
    public List<JsonElement>? Headers { get; set; }

    [JsonPropertyName("rows")]
    public List<List<JsonElement>>? Rows { get; set; }
}

public sealed class WordExportSpec
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("sections")]
    public List<WordSectionSpec>? Sections { get; set; }
}

public sealed class WordSectionSpec
{
    [JsonPropertyName("heading")]
    public string? Heading { get; set; }

    [JsonPropertyName("paragraphs")]
    public List<string>? Paragraphs { get; set; }
}

public sealed record ChatAttachmentDto(
    Guid Id,
    string Filename,
    string MimeType,
    string Format);
