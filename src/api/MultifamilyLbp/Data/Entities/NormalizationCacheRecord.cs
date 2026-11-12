namespace Intranet.Api.MultifamilyLbp.Data.Entities;

public sealed class NormalizationCacheRecord
{
    public Guid Id { get; set; }
    public string FieldName { get; set; } = "component";
    public string OriginalValue { get; set; } = "";
    public string NormalizedValue { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
