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
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Shipped = "shipped";
    public const string Closed = "closed";

    /// <summary>Legacy status stored before the approval workflow. Maps to <see cref="Approved"/>.</summary>
    public const string Planned = "planned";

    /// <summary>Legacy status stored before the approval workflow. Maps to <see cref="Shipped"/>.</summary>
    public const string Done = "done";

    public static readonly string[] Allowed = [New, Approved, Rejected, Shipped, Closed];

    public static string Normalize(string? status) => status switch
    {
        Planned => Approved,
        Done => Shipped,
        _ => status ?? string.Empty,
    };

    public static bool IsValid(string? status)
    {
        var normalized = Normalize(status);
        return Allowed.Contains(normalized, StringComparer.Ordinal);
    }

    public static bool IsTerminal(string? status)
    {
        var normalized = Normalize(status);
        return normalized is Rejected or Closed;
    }

    public static bool CanTransition(string? from, string? to)
    {
        var current = Normalize(from);
        var next = Normalize(to);
        return (current, next) switch
        {
            (New, Approved) => true,
            (New, Rejected) => true,
            (Approved, Shipped) => true,
            (Approved, Rejected) => true,
            (Approved, Closed) => true,
            (Shipped, Closed) => true,
            _ => false,
        };
    }
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

    public string? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ClosedBy { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public bool ViewerCanApprove { get; set; }

    public bool ViewerCanClose { get; set; }
}

public sealed class FeatureRequestMetaDto
{
    public bool ApproverEmailsConfigured { get; set; }

    public bool ViewerCanApprove { get; set; }

    public int ApproverCount { get; set; }
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
