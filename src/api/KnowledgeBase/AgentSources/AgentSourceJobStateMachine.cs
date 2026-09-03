using Intranet.Api.Data.Entities;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public static class AgentSourceJobStateMachine
{
    private static readonly HashSet<(string From, string To)> Allowed =
    [
        (AgentSourceJobStatuses.Queued, AgentSourceJobStatuses.Probing),
        (AgentSourceJobStatuses.Queued, AgentSourceJobStatuses.Running),
        (AgentSourceJobStatuses.Queued, AgentSourceJobStatuses.Failed),
        (AgentSourceJobStatuses.Queued, AgentSourceJobStatuses.AwaitingApproval),
        (AgentSourceJobStatuses.Probing, AgentSourceJobStatuses.Running),
        (AgentSourceJobStatuses.Probing, AgentSourceJobStatuses.Failed),
        (AgentSourceJobStatuses.Probing, AgentSourceJobStatuses.AwaitingApproval),
        (AgentSourceJobStatuses.Running, AgentSourceJobStatuses.Done),
        (AgentSourceJobStatuses.Running, AgentSourceJobStatuses.Failed),
        (AgentSourceJobStatuses.AwaitingApproval, AgentSourceJobStatuses.Queued),
        (AgentSourceJobStatuses.AwaitingApproval, AgentSourceJobStatuses.Failed),
    ];

    public static bool IsKnown(string? status) =>
        status is
            AgentSourceJobStatuses.Queued or
            AgentSourceJobStatuses.Probing or
            AgentSourceJobStatuses.Running or
            AgentSourceJobStatuses.Done or
            AgentSourceJobStatuses.Failed or
            AgentSourceJobStatuses.AwaitingApproval;

    public static bool CanTransition(string from, string to) =>
        Allowed.Contains((from, to));

    public static string Transition(string from, string to)
    {
        if (!IsKnown(from) || !IsKnown(to))
        {
            throw new InvalidOperationException($"Unknown job status '{from}' → '{to}'.");
        }

        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException($"Cannot move an ingest job from '{from}' to '{to}'.");
        }

        return to;
    }

    public static void Apply(AgentSourceJob job, string to, string? errorMessage = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        job.Status = Transition(job.Status, to);
        if (to is AgentSourceJobStatuses.Probing or AgentSourceJobStatuses.Running)
        {
            job.StartedAt ??= DateTimeOffset.UtcNow;
        }

        if (to is AgentSourceJobStatuses.Done or AgentSourceJobStatuses.Failed)
        {
            job.FinishedAt = DateTimeOffset.UtcNow;
        }

        if (to == AgentSourceJobStatuses.Failed)
        {
            job.ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? "Ingest failed."
                : errorMessage.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            job.ErrorMessage = errorMessage.Trim();
        }
    }
}
