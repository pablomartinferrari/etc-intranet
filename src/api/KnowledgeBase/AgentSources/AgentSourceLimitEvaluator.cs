using Intranet.Api.KnowledgeBase.Options;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public enum AgentSourceLimitTier
{
    Soft,
    Medium,
    Hard,
}

public sealed record AgentSourceLimitDecision(
    AgentSourceLimitTier Tier,
    bool CanAutoRun,
    bool RequiresConfirm,
    bool RequiresApproval,
    string Summary);

public static class AgentSourceLimitEvaluator
{
    public static AgentSourceLimitDecision Evaluate(
        int allowedFiles,
        long allowedBytes,
        AgentSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (allowedFiles < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(allowedFiles));
        }

        if (allowedBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(allowedBytes));
        }

        if (allowedFiles <= options.SoftMaxFiles && allowedBytes <= options.SoftMaxBytes)
        {
            return new AgentSourceLimitDecision(
                AgentSourceLimitTier.Soft,
                CanAutoRun: true,
                RequiresConfirm: false,
                RequiresApproval: false,
                Summary: $"Within the automatic limit ({options.SoftMaxFiles:N0} files and {FormatBytes(options.SoftMaxBytes)}). Ingest will start after you connect.");
        }

        if (allowedFiles <= options.MediumMaxFiles && allowedBytes <= options.MediumMaxBytes)
        {
            return new AgentSourceLimitDecision(
                AgentSourceLimitTier.Medium,
                CanAutoRun: false,
                RequiresConfirm: true,
                RequiresApproval: false,
                Summary: $"This folder is larger than the automatic limit ({options.SoftMaxFiles:N0} files / {FormatBytes(options.SoftMaxBytes)}). Confirm to ingest up to {options.MediumMaxFiles:N0} files / {FormatBytes(options.MediumMaxBytes)}.");
        }

        return new AgentSourceLimitDecision(
            AgentSourceLimitTier.Hard,
            CanAutoRun: false,
            RequiresConfirm: false,
            RequiresApproval: true,
            Summary: $"This folder is too large for self-serve ingest (over {options.MediumMaxFiles:N0} files or {FormatBytes(options.MediumMaxBytes)}). An admin approval request will be filed instead.");
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        double value = bytes;
        string[] units = ["KB", "MB", "GB", "TB"];
        var unit = "B";
        foreach (var next in units)
        {
            if (value < 1024)
            {
                break;
            }

            value /= 1024;
            unit = next;
        }

        return $"{value:0.##} {unit}";
    }
}
