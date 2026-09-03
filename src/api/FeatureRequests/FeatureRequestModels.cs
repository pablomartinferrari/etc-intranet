namespace Intranet.Api.FeatureRequests;

public static class FeatureRequestPages
{
    public const string Chat = "chat";
    public const string Lead = "lead";
    public const string Sales = "sales";
    public const string General = "general";
    public const string Other = "other";
    public const string Opportunities = "opportunities";
    public const string Pipeline = "pipeline";

    public const int AreaLabelMaxLength = 120;

    /// <summary>
    /// Intranet areas staff can file against, plus legacy Sales page values
    /// that must stay valid for existing FeatureRequests rows.
    /// </summary>
    public static readonly string[] Allowed =
    [
        Chat,
        Lead,
        Sales,
        General,
        Other,
        Opportunities,
        Pipeline,
    ];

    public static bool IsValid(string? page) =>
        page is not null && Allowed.Contains(page, StringComparer.Ordinal);

    public static bool IsOther(string? page) =>
        string.Equals(page, Other, StringComparison.Ordinal);

    public static string DisplayName(string page, string? areaLabel = null)
    {
        if (IsOther(page) && !string.IsNullOrWhiteSpace(areaLabel))
        {
            return areaLabel.Trim();
        }

        return page switch
        {
            Chat => "Chat",
            Lead => "Lead",
            Sales => "Sales",
            General => "General",
            Other => "Other",
            Opportunities => "Bids",
            Pipeline => "Pipeline",
            _ => page,
        };
    }
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

    /// <summary>
    /// Required when <see cref="Page"/> is <c>other</c>. Ignored for preset areas.
    /// </summary>
    public string? AreaLabel { get; set; }

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

    public string? AreaLabel { get; set; }

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
