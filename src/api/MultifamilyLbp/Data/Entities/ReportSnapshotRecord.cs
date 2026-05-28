namespace Intranet.Api.MultifamilyLbp.Data.Entities;

public sealed class ReportSnapshotRecord
{
    public Guid Id { get; set; }
    public Guid JobEntityId { get; set; }
    public JobEntityRecord JobEntity { get; set; } = null!;
    public string DataType { get; set; } = "units";
    public string ConfigJson { get; set; } = "{}";
    public string ResultJson { get; set; } = "{}";
    public double UniformThreshold { get; set; } = 40;
    public bool UseNormalizedValues { get; set; } = true;
    public string? GeneratedBy { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
}
