namespace Intranet.Api.MultifamilyLbp.Data.Entities;

public sealed class UploadBatchRecord
{
    public Guid Id { get; set; }
    public Guid JobEntityId { get; set; }
    public JobEntityRecord JobEntity { get; set; } = null!;
    public string SourceFileName { get; set; } = "";
    public string? SharePointFileUrl { get; set; }
    public string DataType { get; set; } = "units";
    public string? BuildingProperty { get; set; }
    public DateOnly? InspectionDate { get; set; }
    public string? BatchName { get; set; }
    public string Status { get; set; } = "pending";
    public string? ValidationWarningsJson { get; set; }
    public string? ErrorLogJson { get; set; }
    public int ImportedRowCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<InspectionRowRecord> InspectionRows { get; set; } = [];
}
