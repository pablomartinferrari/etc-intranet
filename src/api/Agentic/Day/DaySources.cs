using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace Intranet.Api.Agentic.Day;

public sealed record DayItem(
    string Id,
    string Title,
    string? Detail,
    DateTimeOffset? When,
    string? Href,
    DateTimeOffset? End = null,
    string? Organizer = null,
    IReadOnlyList<string>? Attendees = null,
    string? Location = null,
    string? TeamsLink = null,
    string? BodyPreview = null);

public sealed record DaySection(string Id, string Title, IReadOnlyList<DayItem> Items);

public sealed record DayQuery(
    WorkspaceActor Actor,
    DateOnly Date,
    string TimeZoneId,
    DateTimeOffset Now);

/// <summary>
/// Pluggable Your Day source. Add a new implementation and register it in
/// <see cref="Intranet.Api.Agentic.AgenticServiceExtensions"/> — the home page
/// does not need to change.
/// </summary>
public interface IDayDataSource
{
    string Name { get; }

    bool IsEnabled { get; }

    Task<IReadOnlyList<DaySection>> GetAsync(DayQuery query, CancellationToken cancellationToken);
}

public interface IDayAggregator
{
    Task<DaySummaryDto> GetAsync(DayQuery query, CancellationToken cancellationToken);
}

public sealed class MockDayDataSource : IDayDataSource
{
    private readonly bool _enabled;

    public MockDayDataSource()
        : this(enabled: true)
    {
    }

    public MockDayDataSource(IHostEnvironment environment)
        : this(environment.IsDevelopment())
    {
    }

    public MockDayDataSource(bool enabled)
    {
        _enabled = enabled;
    }

    public string Name => "mock";

    public bool IsEnabled => _enabled;

    public Task<IReadOnlyList<DaySection>> GetAsync(DayQuery query, CancellationToken cancellationToken)
    {
        var tz = DayTimeZones.Resolve(query.TimeZoneId);
        DateTimeOffset At(int hour, int minute) => DayTimeZones.At(query.Date, hour, minute, tz);
        var you = string.IsNullOrWhiteSpace(query.Actor.DisplayName) ? "You" : query.Actor.DisplayName;

        var sections = new DaySection[]
        {
            new("meetings", "Meetings",
            [
                new DayItem(
                    "m1",
                    "Pipeline standup",
                    "Microsoft Teams · 9:00–9:30",
                    At(9, 0),
                    null,
                    End: At(9, 30),
                    Organizer: you,
                    Attendees: ["User B", "User C"],
                    Location: "Microsoft Teams",
                    TeamsLink: "https://teams.microsoft.com/l/meetup-join/demo-standup",
                    BodyPreview: "Daily pipeline blockers and CLEATUS pursuits."),
                new DayItem(
                    "m2",
                    "Handoff after standup",
                    "Microsoft Teams · 9:30–10:00",
                    At(9, 30),
                    null,
                    End: At(10, 0),
                    Organizer: "User B",
                    Attendees: [you],
                    Location: "Microsoft Teams",
                    TeamsLink: "https://teams.microsoft.com/l/meetup-join/demo-handoff",
                    BodyPreview: null),
                new DayItem(
                    "m3",
                    "Project Alpha review",
                    "Conference A · 11:00–12:00",
                    At(11, 0),
                    "/workspace/projects/" + WorkspaceIds.ProjectAlpha,
                    End: At(12, 0),
                    Organizer: "User B",
                    Attendees: [you, "User C", "User D"],
                    Location: "Conference A",
                    TeamsLink: null,
                    BodyPreview: "Walk the quarterly report draft and close-out reasons. Bring inspection photos."),
                new DayItem(
                    "m4",
                    "1:1 with User B",
                    "15:00–15:30",
                    At(15, 0),
                    null,
                    End: At(15, 30),
                    Organizer: you,
                    Attendees: ["User B"],
                    Location: null,
                    TeamsLink: null,
                    BodyPreview: null),
            ]),
            new("tasks", "Tasks",
            [
                new DayItem("t1", "Send quarterly report draft", "Due today · Folder A", null, "/workspace/repo/" + WorkspaceIds.FolderA),
                new DayItem("t2", "Review close-out reasons", "Pipeline", null, "/pipeline"),
                new DayItem("t3", "Upload inspection photos", "Lead", null, "/lead-inspection"),
                new DayItem("t4", "Share Team chat with Group X", "Workspace", null, "/workspace/chats/" + WorkspaceIds.ChatTeam),
                new DayItem("t5", "Prep Alpha instructions", "Project Alpha", null, "/workspace/projects/" + WorkspaceIds.ProjectAlpha),
            ]),
            new("follow-ups", "Follow-ups",
            [
                new DayItem("f1", "Reply to User C on Team chat", "Waiting since yesterday", null, "/workspace/chats/" + WorkspaceIds.ChatTeam),
                new DayItem("f2", "Confirm Folder B indexing", "Repository", null, "/workspace/repo/" + WorkspaceIds.FolderB),
            ]),
            new("suggestions", "Agent suggestions",
            [
                new DayItem("s1", "Summarize yesterday’s Project Alpha thread", "I can pull citations from Folder A.", null, "/workspace/chats/" + WorkspaceIds.ChatPlaceholder),
                new DayItem("s2", "Draft a follow-up after the 11:00 review", "Uses your day calendar + Alpha instructions.", null, null),
                new DayItem("s3", "Find last quarter’s report in the repository", "Search is scoped to Folder A.", null, "/workspace/search?q=quarterly%20report&folderId=" + WorkspaceIds.FolderA),
            ]),
        };
        return Task.FromResult<IReadOnlyList<DaySection>>(sections);
    }
}

public sealed class DayAggregator : IDayAggregator
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(90);

    private readonly IEnumerable<IDayDataSource> _sources;
    private readonly IUserGraphCalendarClient _calendar;
    private readonly IUserGraphMailClient _mail;
    private readonly IMeetingBriefGenerator _brief;
    private readonly IMemoryCache _cache;
    private readonly IHostEnvironment _environment;

    public DayAggregator(
        IEnumerable<IDayDataSource> sources,
        IUserGraphCalendarClient calendar,
        IUserGraphMailClient mail,
        IMeetingBriefGenerator brief,
        IMemoryCache cache,
        IHostEnvironment environment)
    {
        _sources = sources;
        _calendar = calendar;
        _mail = mail;
        _brief = brief;
        _cache = cache;
        _environment = environment;
    }

    public async Task<DaySummaryDto> GetAsync(DayQuery query, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(query);
        if (_cache.TryGetValue(cacheKey, out DaySummaryDto? cached) && cached is not null)
        {
            return cached;
        }

        var sections = new Dictionary<string, DaySection>(StringComparer.OrdinalIgnoreCase);
        var used = new List<string>();
        var allowMock = !_environment.IsProduction();
        foreach (var source in _sources)
        {
            if (!source.IsEnabled)
            {
                continue;
            }

            if (!allowMock && string.Equals(source.Name, "mock", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var batch = await source.GetAsync(query, cancellationToken);
            if (batch.Count == 0)
            {
                continue;
            }

            used.Add(source.Name);
            foreach (var section in batch)
            {
                sections[section.Id] = section;
            }
        }

        var calendar = await ResolveCalendarAsync(query, sections, used, cancellationToken);
        var mail = await ResolveMailAsync(query, sections, used, cancellationToken);
        if (!allowMock)
        {
            sections.Remove("tasks");
            sections.Remove("suggestions");
        }

        var meetings = MeetingsFrom(sections);
        var brief = await _brief.GenerateAsync(meetings, query, cancellationToken);

        var ordered = new[] { "meetings", "tasks", "follow-ups", "suggestions" }
            .Where(sections.ContainsKey)
            .Select(id => ToDto(sections[id]))
            .ToList();

        var hour = query.Now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
        var summary = new DaySummaryDto(
            greeting,
            query.Actor.DisplayName,
            query.Date,
            query.TimeZoneId,
            ordered,
            used,
            brief,
            calendar,
            mail);

        _cache.Set(cacheKey, summary, CacheDuration);
        return summary;
    }

    public static string CacheKey(DayQuery query) =>
        $"agentic:day:{query.Actor.Oid}:{query.Date:yyyy-MM-dd}:{query.TimeZoneId}";

    private async Task<CalendarStatusDto> ResolveCalendarAsync(
        DayQuery query,
        Dictionary<string, DaySection> sections,
        List<string> used,
        CancellationToken cancellationToken)
    {
        var production = _environment.IsProduction();
        var demo = query.Actor.IsDemoIdentity;
        if (demo && !production)
        {
            return new CalendarStatusDto(CalendarReadStates.Mock, null);
        }

        var result = await _calendar.GetTodaysMeetingsAsync(query, cancellationToken);
        if (production && result.State == CalendarReadStates.Mock)
        {
            result = new CalendarDayResult(
                CalendarReadStates.Unavailable,
                "Sample calendar data is disabled in production. Sign in with Microsoft Entra and set AzureAd:ClientSecret so Graph can read your Outlook calendar.",
                []);
        }

        if (!used.Contains("graph-calendar", StringComparer.OrdinalIgnoreCase))
        {
            used.Insert(0, "graph-calendar");
        }

        sections["meetings"] = result.State == CalendarReadStates.Ok
            ? ToSection(result.Meetings)
            : new DaySection("meetings", "Meetings", []);
        return new CalendarStatusDto(result.State, result.Message);
    }

    private async Task<MailStatusDto> ResolveMailAsync(
        DayQuery query,
        Dictionary<string, DaySection> sections,
        List<string> used,
        CancellationToken cancellationToken)
    {
        var production = _environment.IsProduction();
        var demo = query.Actor.IsDemoIdentity;
        if (demo && !production)
        {
            return new MailStatusDto(MailReadStates.Mock, null);
        }

        var result = await _mail.GetFollowUpsAsync(query, cancellationToken);
        if (production && result.State == MailReadStates.Mock)
        {
            result = new MailDayResult(
                MailReadStates.Unavailable,
                "Sample follow-ups are disabled in production. Grant delegated Mail.Read (admin consent) on the intranet web API app registration.",
                []);
        }

        if (!used.Contains("graph-mail", StringComparer.OrdinalIgnoreCase)
            && result.State is MailReadStates.Ok or MailReadStates.ConsentRequired or MailReadStates.Unavailable)
        {
            used.Add("graph-mail");
        }

        sections["follow-ups"] = result.State == MailReadStates.Ok
            ? ToMailSection(result.Items)
            : new DaySection("follow-ups", "Follow-ups", []);
        return new MailStatusDto(result.State, result.Message);
    }

    private static DaySection ToMailSection(IReadOnlyList<MailFollowUp> items) =>
        new("follow-ups", "Follow-ups", items.Select(ToMailItem).ToList());

    public static DayItem ToMailItem(MailFollowUp item)
    {
        var when = item.Received is { } received
            ? received.ToLocalTime().ToString("MMM d h:mm tt")
            : null;
        var from = item.SenderName ?? item.SenderEmail;
        var kind = item.Kind == "flagged" ? "Flagged" : "Needs a reply";
        var detail = string.Join(
            " · ",
            new[] { from is null ? null : "From " + from, when, kind }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        return new DayItem(
            item.Id,
            item.Subject,
            string.IsNullOrWhiteSpace(detail) ? null : detail,
            item.Received,
            item.WebLink,
            Organizer: from,
            BodyPreview: item.NeedFromYou ?? item.BodyPreview);
    }

    private static IReadOnlyList<DayMeeting> MeetingsFrom(IReadOnlyDictionary<string, DaySection> sections)
    {
        if (!sections.TryGetValue("meetings", out var section))
        {
            return [];
        }

        return section.Items.Select(ToMeeting).ToList();
    }

    private static DaySection ToSection(IReadOnlyList<DayMeeting> meetings) =>
        new("meetings", "Meetings", meetings.Select(ToItem).ToList());

    public static DayItem ToItem(DayMeeting meeting)
    {
        var window = meeting.Start is null
            ? meeting.Location
            : DayTimeZones.FormatWindow(meeting.Start, meeting.End);
        var detail = string.Join(
            " · ",
            new[] { meeting.Location, window }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        return new DayItem(
            meeting.Id,
            meeting.Subject,
            string.IsNullOrWhiteSpace(detail) ? null : detail,
            meeting.Start,
            meeting.WebLink,
            End: meeting.End,
            Organizer: meeting.Organizer,
            Attendees: meeting.Attendees,
            Location: meeting.Location,
            TeamsLink: meeting.TeamsLink,
            BodyPreview: meeting.BodyPreview);
    }

    public static DayMeeting ToMeeting(DayItem item) =>
        new(
            item.Id,
            item.Title,
            item.When,
            item.End,
            item.Organizer,
            item.Attendees ?? [],
            item.Location,
            item.TeamsLink,
            item.BodyPreview,
            item.Href);

    private static DaySectionDto ToDto(DaySection section) =>
        new(
            section.Id,
            section.Title,
            section.Items.Select(i => new DayItemDto(
                i.Id,
                i.Title,
                i.Detail,
                i.When,
                i.Href,
                i.End,
                i.Organizer,
                i.Attendees,
                i.Location,
                i.TeamsLink,
                i.BodyPreview)).ToList());
}
