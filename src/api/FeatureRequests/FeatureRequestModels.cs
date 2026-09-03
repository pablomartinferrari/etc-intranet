namespace Intranet.Api.FeatureRequests;

public static class FeatureRequestPages
{
    public static readonly string[] Allowed = ["sales", "opportunities", "pipeline"];

    public static bool IsValid(string? page) =>
        page is not null && Allowed.Contains(page, StringComparer.Ordinal);
}

public static class FeatureRequestStatuses
{
    public const string New = "new";
    public const string Planned = "planned";
    public const string Done = "done";

    public static readonly string[] Allowed = [New, Planned, Done];

    public static bool IsValid(string? status) =>
        status is not null && Allowed.Contains(status, StringComparer.Ordinal);
}

public sealed class CreateFeatureRequestBody
{
    public string? Page { get; set; }

    public string? RawText { get; set; }
}

public sealed class UpdateFeatureRequestStatusBody
{
    public string? Status { get; set; }
}

public sealed class FeatureRequestDto
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

public sealed class StructuredTicket
{
    public required string Title { get; set; }

    public required string Problem { get; set; }

    public required string DesiredBehavior { get; set; }

    public required string DataInvolved { get; set; }

    public required string AcceptanceCriteria { get; set; }

    public required string StructuredBy { get; set; }
}
