namespace Intranet.Api.MultifamilyLbp.Data.Entities;

public sealed class InspectionRowRecord
{
    public Guid Id { get; set; }
    public Guid JobEntityId { get; set; }
    public JobEntityRecord JobEntity { get; set; } = null!;
    public Guid? UploadBatchId { get; set; }
    public UploadBatchRecord? UploadBatch { get; set; }

    public string ReadingId { get; set; } = "";
    public string SourceFileName { get; set; } = "";
    public string DataType { get; set; } = "units";
    public string Location { get; set; } = "";
    public string? RoomOrArea { get; set; }
    public string Component { get; set; } = "";
    public string? NormalizedComponent { get; set; }
    public string? Substrate { get; set; }
    public string? NormalizedSubstrate { get; set; }
    public int ShotCount { get; set; } = 1;
    public string? Notes { get; set; }
    public string ValidationStatus { get; set; } = "clean";

    public string Color { get; set; } = "";
    public double LeadContent { get; set; }
    public bool IsPositive { get; set; }
    public string? UnitNumber { get; set; }
    public string? Multifamily { get; set; }
    public string? SiteAddress { get; set; }
    public string? RoomType { get; set; }
    public string? RoomNumber { get; set; }
    public string? Side { get; set; }
    public string? Condition { get; set; }
    public string? Timestamp { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
