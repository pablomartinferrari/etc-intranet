namespace Intranet.Api.Cleat;

public static class PipelineCloseoutRules
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(21);

    public static bool IsClosed(PursuitDto pursuit)
    {
        if (pursuit.Archived)
        {
            return true;
        }

        return IsWonOrLost(pursuit.Phase) || IsWonOrLost(pursuit.ColumnTitle);
    }

    public static (bool NeedsCloseOut, IReadOnlyList<string> Reasons) Evaluate(
        PursuitDto pursuit,
        DateTimeOffset now)
    {
        if (IsClosed(pursuit))
        {
            return (false, []);
        }

        var reasons = new List<string>();
        var deadline = ParseTimestamp(pursuit.DeadlineDate);

        if (deadline is not null && deadline.Value < now)
        {
            reasons.Add("deadline_passed");
        }

        var stage = Normalize(pursuit.Phase) ?? Normalize(pursuit.ColumnTitle);
        var inTrackedStage = stage is "triage" or "preparing" or "submitted";
        if (pursuit.LastActivityAvailable
            && inTrackedStage
            && ParseTimestamp(pursuit.LastActivityAt) is { } last
            && now - last >= StaleAfter)
        {
            reasons.Add("stale_21_days");
        }

        // OpenAPI/Zapier do not document a pursuit last-activity field. When we cannot
        // measure staleness, still surface rows that have no deadline so they do not hide.
        if (!pursuit.LastActivityAvailable && deadline is null)
        {
            reasons.Add("no_deadline_on_file");
        }

        return (reasons.Count > 0, reasons);
    }

    public static bool IsWonOrLost(string? value)
    {
        var normalized = Normalize(value);
        return normalized is "won" or "lost";
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    public static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }
}
