using System.Text.Json.Serialization;

namespace Intranet.Api.MultifamilyLbp.Models;

/// <summary>Aligned with SPFx IXrfReading (camelCase JSON).</summary>
public sealed class XrfReadingDto
{
    [JsonPropertyName("readingId")]
    public string ReadingId { get; set; } = "";

    [JsonPropertyName("component")]
    public string Component { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "";

    [JsonPropertyName("leadContent")]
    public double LeadContent { get; set; }

    [JsonPropertyName("normalizedComponent")]
    public string? NormalizedComponent { get; set; }

    [JsonPropertyName("normalizedSubstrate")]
    public string? NormalizedSubstrate { get; set; }

    [JsonPropertyName("isPositive")]
    public bool IsPositive { get; set; }

    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("unitNumber")]
    public string? UnitNumber { get; set; }

    [JsonPropertyName("multifamily")]
    public string? Multifamily { get; set; }

    [JsonPropertyName("siteAddress")]
    public string? SiteAddress { get; set; }

    [JsonPropertyName("roomType")]
    public string? RoomType { get; set; }

    [JsonPropertyName("roomNumber")]
    public string? RoomNumber { get; set; }

    [JsonPropertyName("substrate")]
    public string? Substrate { get; set; }

    [JsonPropertyName("side")]
    public string? Side { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("areaType")]
    public string? AreaType { get; set; }

    [JsonPropertyName("rawRow")]
    public System.Text.Json.JsonElement? RawRow { get; set; }
}
