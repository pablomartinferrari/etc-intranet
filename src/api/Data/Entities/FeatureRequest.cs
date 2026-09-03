namespace Intranet.Api.Data.Entities;

public class FeatureRequest
{
    public int Id { get; set; }

    public required string Page { get; set; }

    public required string CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public required string RawText { get; set; }

    public required string Title { get; set; }

    public required string Problem { get; set; }

    public required string DesiredBehavior { get; set; }

    public required string DataInvolved { get; set; }

    public required string AcceptanceCriteria { get; set; }

    public required string Status { get; set; }

    public required string StructuredBy { get; set; }
}
