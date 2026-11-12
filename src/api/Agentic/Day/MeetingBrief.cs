using System.Globalization;
using System.Text;
using System.Text.Json;
using Intranet.Api.KnowledgeBase.Services;

namespace Intranet.Api.Agentic.Day;

public interface IMeetingBriefGenerator
{
    Task<MeetingBriefDto> GenerateAsync(
        IReadOnlyList<DayMeeting> meetings,
        DayQuery query,
        CancellationToken cancellationToken);
}

public static class DeterministicMeetingBrief
{
    public const string Source = "deterministic";

    public static MeetingBriefDto Build(IReadOnlyList<DayMeeting> meetings, DayQuery query)
    {
        var highlights = new List<string>();
        if (meetings.Count == 0)
        {
            return new MeetingBriefDto(
                "No meetings on the calendar today. You have the day open.",
                Source,
                ["Open day"]);
        }

        var timed = meetings
            .Where(m => m.Start.HasValue)
            .OrderBy(m => m.Start)
            .ToList();
        var upcoming = timed.FirstOrDefault(m => m.Start >= query.Now);
        if (upcoming is not null)
        {
            highlights.Add($"Next: {upcoming.Subject} at {DayTimeZones.FormatClock(upcoming.Start)}");
        }
        else if (timed.Count > 0)
        {
            highlights.Add($"Last on the calendar: {timed[^1].Subject}");
        }

        var conflicts = FindOverlaps(timed);
        if (conflicts.Count > 0)
        {
            highlights.Add(conflicts.Count == 1
                ? $"Conflict: {conflicts[0]}"
                : $"{conflicts.Count} overlapping meetings");
        }

        var backToBack = FindBackToBack(timed, TimeSpan.FromMinutes(5));
        if (backToBack.Count > 0)
        {
            highlights.Add(backToBack.Count == 1
                ? $"Back-to-back: {backToBack[0]}"
                : $"{backToBack.Count} back-to-back transitions");
        }

        var prep = meetings.Where(NeedsPrep).Select(m => m.Subject).Distinct().Take(3).ToList();
        if (prep.Count > 0)
        {
            highlights.Add("Prep: " + string.Join(", ", prep));
        }

        var free = FindFreeBlocks(timed, query.Date, DayTimeZones.Resolve(query.TimeZoneId), TimeSpan.FromMinutes(45));
        if (free.Count > 0)
        {
            highlights.Add("Free: " + string.Join(", ", free.Take(3)));
        }
        else if (timed.Count > 0)
        {
            highlights.Add("Little open time between 8:00 and 17:00");
        }

        var text = Compose(meetings, upcoming, conflicts, backToBack, prep, free, query);
        return new MeetingBriefDto(text, Source, highlights);
    }

    private static string Compose(
        IReadOnlyList<DayMeeting> meetings,
        DayMeeting? upcoming,
        IReadOnlyList<string> conflicts,
        IReadOnlyList<string> backToBack,
        IReadOnlyList<string> prep,
        IReadOnlyList<string> free,
        DayQuery query)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{meetings.Count} meeting{(meetings.Count == 1 ? "" : "s")} today");
        if (upcoming is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $". Up next is {upcoming.Subject} at {DayTimeZones.FormatClock(upcoming.Start)}");
            if (!string.IsNullOrWhiteSpace(upcoming.Location))
            {
                sb.Append(CultureInfo.InvariantCulture, $" ({upcoming.Location})");
            }
        }
        else
        {
            sb.Append(". Nothing left later in the day");
        }

        if (conflicts.Count > 0)
        {
            sb.Append(". ").Append(conflicts.Count == 1 ? "Overlap: " : "Overlaps: ").Append(string.Join("; ", conflicts));
        }

        if (backToBack.Count > 0)
        {
            sb.Append(". Back-to-back: ").Append(string.Join("; ", backToBack));
        }

        if (prep.Count > 0)
        {
            sb.Append(". Prep needed for ").Append(string.Join(", ", prep));
        }

        if (free.Count > 0)
        {
            sb.Append(". Open blocks ").Append(string.Join(", ", free));
        }

        sb.Append('.');
        return sb.ToString();
    }

    public static IReadOnlyList<string> FindOverlaps(IReadOnlyList<DayMeeting> timed)
    {
        var found = new List<string>();
        for (var i = 0; i < timed.Count; i++)
        {
            for (var j = i + 1; j < timed.Count; j++)
            {
                var a = timed[i];
                var b = timed[j];
                if (a.Start is null || b.Start is null || a.End is null)
                {
                    continue;
                }

                if (b.Start < a.End)
                {
                    found.Add($"{a.Subject} / {b.Subject}");
                }
            }
        }

        return found;
    }

    public static IReadOnlyList<string> FindBackToBack(IReadOnlyList<DayMeeting> timed, TimeSpan slack)
    {
        var found = new List<string>();
        for (var i = 0; i < timed.Count - 1; i++)
        {
            var current = timed[i];
            var next = timed[i + 1];
            if (current.End is null || next.Start is null)
            {
                continue;
            }

            var gap = next.Start.Value - current.End.Value;
            if (gap >= TimeSpan.Zero && gap <= slack)
            {
                found.Add($"{current.Subject} → {next.Subject}");
            }
        }

        return found;
    }

    public static IReadOnlyList<string> FindFreeBlocks(
        IReadOnlyList<DayMeeting> timed,
        DateOnly date,
        TimeZoneInfo timeZone,
        TimeSpan minimum)
    {
        var workStart = DayTimeZones.At(date, 8, 0, timeZone);
        var workEnd = DayTimeZones.At(date, 17, 0, timeZone);
        var cursor = workStart;
        var blocks = new List<string>();
        foreach (var meeting in timed)
        {
            var start = meeting.Start ?? cursor;
            var end = meeting.End ?? start;
            if (start > cursor)
            {
                var gap = start - cursor;
                if (gap >= minimum && start > workStart && cursor < workEnd)
                {
                    var from = cursor < workStart ? workStart : cursor;
                    var to = start > workEnd ? workEnd : start;
                    if (to - from >= minimum)
                    {
                        blocks.Add($"{DayTimeZones.FormatClock(from)}–{DayTimeZones.FormatClock(to)}");
                    }
                }
            }

            if (end > cursor)
            {
                cursor = end;
            }
        }

        if (workEnd - cursor >= minimum)
        {
            var from = cursor < workStart ? workStart : cursor;
            if (workEnd - from >= minimum)
            {
                blocks.Add($"{DayTimeZones.FormatClock(from)}–{DayTimeZones.FormatClock(workEnd)}");
            }
        }

        return blocks;
    }

    public static bool NeedsPrep(DayMeeting meeting)
    {
        if (!string.IsNullOrWhiteSpace(meeting.BodyPreview))
        {
            return true;
        }

        if (meeting.Attendees.Count >= 4)
        {
            return true;
        }

        var subject = meeting.Subject ?? "";
        return subject.Contains("review", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("prep", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("interview", StringComparison.OrdinalIgnoreCase)
            || subject.Contains("demo", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Uses the same Ollama → hosted Azure OpenAI / OpenAI router as /knowledge.
/// Falls back to <see cref="DeterministicMeetingBrief"/> when no model is available.
/// </summary>
public sealed class MeetingBriefGenerator : IMeetingBriefGenerator
{
    private const string SystemPrompt =
        "You summarize one signed-in user's Outlook day for an intranet home page. " +
        "Use only the meetings JSON. Do not invent meetings, people, or times. " +
        "Cover: what is coming up next, back-to-back or overlapping meetings, prep suggested by titles or bodyPreview, and free blocks. " +
        "Write 3-6 short sentences. No greeting. No mention of other mailboxes.";

    private readonly ChatCompletionRouter _chat;
    private readonly ILogger<MeetingBriefGenerator> _logger;

    public MeetingBriefGenerator(ChatCompletionRouter chat, ILogger<MeetingBriefGenerator> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    public async Task<MeetingBriefDto> GenerateAsync(
        IReadOnlyList<DayMeeting> meetings,
        DayQuery query,
        CancellationToken cancellationToken)
    {
        var fallback = DeterministicMeetingBrief.Build(meetings, query);
        if (meetings.Count == 0)
        {
            return fallback;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));
            var result = await _chat.CompleteAsync(SystemPrompt, UserPrompt(meetings, query), cts.Token);
            var text = result.Content?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length < 20)
            {
                return fallback;
            }

            return new MeetingBriefDto(text, "llm", fallback.Highlights);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "Meeting brief LLM unavailable; using deterministic summary.");
            return fallback;
        }
    }

    private static string UserPrompt(IReadOnlyList<DayMeeting> meetings, DayQuery query)
    {
        var payload = meetings.Select(m => new
        {
            m.Subject,
            start = m.Start?.ToString("o"),
            end = m.End?.ToString("o"),
            m.Organizer,
            attendees = m.Attendees,
            m.Location,
            hasTeams = !string.IsNullOrWhiteSpace(m.TeamsLink),
            m.BodyPreview,
        });
        return
            $"Date: {query.Date:yyyy-MM-dd}\nTime zone: {query.TimeZoneId}\nNow: {query.Now:o}\nMeetings:\n" +
            JsonSerializer.Serialize(payload);
    }
}
