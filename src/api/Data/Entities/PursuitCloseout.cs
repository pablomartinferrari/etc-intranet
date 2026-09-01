namespace Intranet.Api.Data.Entities;

public class PursuitCloseout
{
    public int Id { get; set; }

    public required string PursuitId { get; set; }

    public string? OpportunityId { get; set; }

    public required string Outcome { get; set; }

    public string? ReasonCode { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CleatusSyncedAt { get; set; }
}
