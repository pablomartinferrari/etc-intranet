namespace Intranet.Api.MultifamilyLbp.Data.Entities;

public sealed class JobEntityRecord
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public JobRecord Job { get; set; } = null!;
    public string EntitySlug { get; set; } = "";
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<UploadBatchRecord> UploadBatches { get; set; } = [];
    public ICollection<InspectionRowRecord> InspectionRows { get; set; } = [];
    public ICollection<NormalizationSuggestionRecord> NormalizationSuggestions { get; set; } = [];
    public ICollection<ReportSnapshotRecord> ReportSnapshots { get; set; } = [];
}
