namespace Intranet.Api.MultifamilyLbp.Data.Entities;

public sealed class NormalizationSuggestionRecord
{
    public Guid Id { get; set; }
    public Guid JobEntityId { get; set; }
    public JobEntityRecord JobEntity { get; set; } = null!;
    public string FieldName { get; set; } = "component";
    public string OriginalValue { get; set; } = "";
    public string SuggestedValue { get; set; } = "";
    public string? ApprovedValue { get; set; }
    public int AffectedRowCount { get; set; }
    public string DataType { get; set; } = "both";
    public string Confidence { get; set; } = "medium";
    public string Status { get; set; } = "pending";
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
