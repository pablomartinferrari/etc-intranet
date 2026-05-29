namespace Intranet.Api.MultifamilyLbp.Models;

/// <summary>Parsed readings from one SharePoint list item (workbook).</summary>
public sealed record SharePointFileReadings(
    string ItemId,
    string FileName,
    string AreaType,
    IReadOnlyList<XrfReadingDto> Readings);
