namespace Intranet.Api.MultifamilyLbp.Data.Entities;

public sealed class JobRecord
{
    public Guid Id { get; set; }
    public string JobIdentifier { get; set; } = "";
    public string? ClientName { get; set; }
    public string? FacilityName { get; set; }
    public string? FacilityAddress { get; set; }
    public string? JobStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<JobEntityRecord> Entities { get; set; } = [];
}
