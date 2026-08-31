namespace Intranet.Api.Cleat;

public sealed class OpportunityDto
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public string? Agency { get; init; }
    public string? Naics { get; init; }
    public double? Score { get; init; }
    public string? PostedDate { get; init; }
    public string? DeadlineDate { get; init; }
    public string? SolicitationNumber { get; init; }
    public string? SetAside { get; init; }
    public string? Summary { get; init; }
    public string? Overview { get; init; }
    public string? Description { get; init; }
    public string? ResponseType { get; init; }
    public string? OpportunityType { get; init; }
    public string? PlaceOfPerformance { get; init; }
    public string? MatchReason { get; init; }
    public bool? InPipeline { get; init; }
    public string? CleatusUrl { get; init; }
    public string? SourceUrl { get; init; }
}

public sealed class RecommendationListDto
{
    public required IReadOnlyList<OpportunityDto> Items { get; init; }
    public bool HasMore { get; init; }
    public string? NextCursor { get; init; }
}

public sealed class CleatErrorResponse
{
    public required string Error { get; init; }
    public required string Message { get; init; }
}
