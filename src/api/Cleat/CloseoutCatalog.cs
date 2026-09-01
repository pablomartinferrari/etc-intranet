namespace Intranet.Api.Cleat;

public static class CloseoutCatalog
{
    public const string OutcomeWon = "won";
    public const string OutcomeLost = "lost";
    public const string OutcomeDropped = "dropped";

    public static readonly IReadOnlySet<string> Outcomes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            OutcomeWon, OutcomeLost, OutcomeDropped,
        };

    public static readonly IReadOnlySet<string> LostOrDroppedReasons =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "price",
            "past_performance",
            "capacity",
            "missed_deadline",
            "out_of_naics_or_geo",
            "customer_cancelled",
            "other",
        };

    public static readonly IReadOnlySet<string> WonReasons =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "relationship",
            "price",
            "past_performance",
            "other",
        };

    public static string? Validate(string? outcome, string? reasonCode, bool requireLostReason)
    {
        if (string.IsNullOrWhiteSpace(outcome) || !Outcomes.Contains(outcome))
        {
            return "Outcome must be won, lost, or dropped (no longer pursuing).";
        }

        var normalized = outcome.Trim().ToLowerInvariant();
        if (normalized is OutcomeLost or OutcomeDropped)
        {
            if (requireLostReason && string.IsNullOrWhiteSpace(reasonCode))
            {
                return "A reason is required when marking lost or no longer pursuing.";
            }

            if (!string.IsNullOrWhiteSpace(reasonCode) && !LostOrDroppedReasons.Contains(reasonCode))
            {
                return "Unknown close-out reason.";
            }
        }
        else if (!string.IsNullOrWhiteSpace(reasonCode) && !WonReasons.Contains(reasonCode))
        {
            return "Unknown win reason.";
        }

        return null;
    }
}
