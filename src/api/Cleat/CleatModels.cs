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

public sealed class PursuitDto
{
    public required string Id { get; init; }
    public string? OpportunityId { get; init; }
    public string? Title { get; init; }
    public string? Agency { get; init; }
    public string? Phase { get; init; }
    public string? ColumnTitle { get; init; }
    public bool Archived { get; init; }
    public bool? Favorite { get; init; }
    public string? DeadlineDate { get; init; }
    public string? PostedDate { get; init; }
    public string? SolicitationNumber { get; init; }
    public string? Naics { get; init; }
    public string? SetAside { get; init; }
    public string? Summary { get; init; }
    public string? Overview { get; init; }
    public string? Description { get; init; }
    public string? Assignee { get; init; }
    public string? CreatedAt { get; init; }
    public string? LastActivityAt { get; init; }
    public bool LastActivityAvailable { get; init; }
    public string? CleatusUrl { get; init; }
    public string? SourceUrl { get; init; }
}

public sealed class PursuitListDto
{
    public required IReadOnlyList<PursuitDto> Items { get; init; }
    public bool HasMore { get; init; }
    public string? NextCursor { get; init; }
}

public sealed class CloseoutDto
{
    public required string PursuitId { get; init; }
    public string? OpportunityId { get; init; }
    public required string Outcome { get; init; }
    public string? ReasonCode { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? CleatusSyncedAt { get; init; }
}

public sealed class PipelineItemDto
{
    public required PursuitDto Pursuit { get; init; }
    public bool NeedsCloseOut { get; init; }
    public IReadOnlyList<string> CloseOutReasons { get; init; } = [];
    public CloseoutDto? Closeout { get; init; }
}

public sealed class PipelineDashboardDto
{
    public required IReadOnlyList<PipelineItemDto> Items { get; init; }
    public required IReadOnlyList<PipelineItemDto> NeedsCloseOut { get; init; }
    public required PipelineCountsDto Counts { get; init; }
    public bool LastActivityFieldFound { get; init; }
    public bool AssigneeFieldFound { get; init; }
}

public sealed class PipelineCountsDto
{
    public int Triage { get; init; }
    public int Preparing { get; init; }
    public int Submitted { get; init; }
    public int Won { get; init; }
    public int Lost { get; init; }
    public int Archived { get; init; }
    public int Other { get; init; }
    public int Total { get; init; }
}

public sealed class CloseoutRequest
{
    public string? Outcome { get; init; }
    public string? ReasonCode { get; init; }
    public string? Note { get; init; }
    public string? OpportunityId { get; init; }
}

public sealed class CloseoutResponse
{
    public string? Error { get; init; }
    public string? Message { get; init; }
    public bool CleatusUpdated { get; init; }
    public required CloseoutDto Closeout { get; init; }
}

