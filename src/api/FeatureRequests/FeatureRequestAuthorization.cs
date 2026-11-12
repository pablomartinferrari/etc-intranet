namespace Intranet.Api.FeatureRequests;

public static class FeatureRequestAuthorization
{
    public static IReadOnlyList<string> ParseApproverEmails(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => value.Contains('@', StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static bool EmailsMatch(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool IsApprover(FeatureRequestActor actor, IReadOnlyList<string> approverEmails)
    {
        if (!actor.IsAuthenticated || approverEmails.Count == 0)
        {
            return false;
        }

        return approverEmails.Any(email => EmailsMatch(email, actor.Email));
    }

    public static bool IsRequester(FeatureRequestActor actor, string? createdBy)
    {
        if (!actor.IsAuthenticated || string.IsNullOrWhiteSpace(createdBy))
        {
            return false;
        }

        var stored = createdBy.Trim();
        return EmailsMatch(actor.Email, stored)
            || ValuesMatch(actor.ObjectId, stored)
            || ValuesMatch(actor.Name, stored);
    }

    public static bool CanApproveOrReject(
        FeatureRequestActor actor,
        IReadOnlyList<string> approverEmails,
        bool isProduction,
        out string error,
        out string message)
    {
        if (!actor.IsAuthenticated)
        {
            error = "not_approver";
            message = "Sign in to approve or reject a request.";
            return false;
        }

        if (approverEmails.Count == 0)
        {
            if (isProduction)
            {
                error = "approvers_not_configured";
                message = "Set FeatureRequests__ApproverEmails before approving or rejecting in Production.";
                return false;
            }

            error = string.Empty;
            message = string.Empty;
            return true;
        }

        if (IsApprover(actor, approverEmails))
        {
            error = string.Empty;
            message = string.Empty;
            return true;
        }

        error = "not_approver";
        message = "Only configured approvers can approve or reject.";
        return false;
    }

    public static bool CanShip(FeatureRequestActor actor, out string error, out string message)
    {
        if (!actor.IsAuthenticated)
        {
            error = "not_builder";
            message = "Sign in to mark a request shipped.";
            return false;
        }

        error = string.Empty;
        message = string.Empty;
        return true;
    }

    public static bool CanClose(
        FeatureRequestActor actor,
        string? createdBy,
        IReadOnlyList<string> approverEmails,
        bool isProduction,
        out string error,
        out string message)
    {
        if (!actor.IsAuthenticated)
        {
            error = "not_requester_or_approver";
            message = "Sign in to confirm or close a request.";
            return false;
        }

        if (IsRequester(actor, createdBy))
        {
            error = string.Empty;
            message = string.Empty;
            return true;
        }

        if (CanApproveOrReject(actor, approverEmails, isProduction, out _, out _))
        {
            error = string.Empty;
            message = string.Empty;
            return true;
        }

        error = "not_requester_or_approver";
        message = "Only the original requester or an approver can confirm or close this request.";
        return false;
    }

    public static bool CanChangeStatus(
        string currentStatus,
        string nextStatus,
        FeatureRequestActor actor,
        string? createdBy,
        IReadOnlyList<string> approverEmails,
        bool isProduction,
        out string error,
        out string message)
    {
        var from = FeatureRequestStatuses.Normalize(currentStatus);
        var to = FeatureRequestStatuses.Normalize(nextStatus);

        if (!FeatureRequestStatuses.IsValid(nextStatus))
        {
            error = "invalid_status";
            message = "Status must be new, approved, rejected, shipped, or closed.";
            return false;
        }

        if (!FeatureRequestStatuses.CanTransition(from, to))
        {
            error = "invalid_transition";
            message = FeatureRequestStatuses.IsTerminal(from)
                ? "This request is closed and cannot change status."
                : $"Cannot change status from {from} to {to}.";
            return false;
        }

        if (from == FeatureRequestStatuses.New
            && to is FeatureRequestStatuses.Approved or FeatureRequestStatuses.Rejected)
        {
            return CanApproveOrReject(actor, approverEmails, isProduction, out error, out message);
        }

        if (from == FeatureRequestStatuses.Approved && to == FeatureRequestStatuses.Shipped)
        {
            return CanShip(actor, out error, out message);
        }

        if (from == FeatureRequestStatuses.Approved
            && to is FeatureRequestStatuses.Rejected or FeatureRequestStatuses.Closed)
        {
            return CanApproveOrReject(actor, approverEmails, isProduction, out error, out message);
        }

        if (from == FeatureRequestStatuses.Shipped && to == FeatureRequestStatuses.Closed)
        {
            return CanClose(actor, createdBy, approverEmails, isProduction, out error, out message);
        }

        error = "invalid_transition";
        message = $"Cannot change status from {from} to {to}.";
        return false;
    }

    private static bool ValuesMatch(string? left, string right) =>
        !string.IsNullOrWhiteSpace(left)
        && string.Equals(left.Trim(), right, StringComparison.OrdinalIgnoreCase);
}
