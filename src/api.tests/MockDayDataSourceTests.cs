using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Day;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Intranet.Api.Tests;

public class MockDayDataSourceTests
{
    [Fact]
    public async Task AggregatorMergesMockSectionsInWireframeOrder()
    {
        var aggregator = DayTestHarness.Aggregator();
        var actor = new WorkspaceActor("dev-you", "You", "you@etc.local");
        var summary = await aggregator.GetAsync(DayTestHarness.Query(actor), CancellationToken.None);

        Assert.Equal(["meetings", "tasks", "follow-ups", "suggestions"], summary.Sections.Select(s => s.Id).ToArray());
        Assert.Contains("mock", summary.Sources);
        Assert.Equal("You", summary.UserDisplayName);
        Assert.Equal(CalendarReadStates.Mock, summary.Calendar.State);
        Assert.True(summary.Sections.First(s => s.Id == "meetings").Items.Count >= 3);
        Assert.True(summary.Sections.First(s => s.Id == "tasks").Items.Count >= 5);
        Assert.NotNull(summary.Brief);
        Assert.Contains("meeting", summary.Brief!.Text, StringComparison.OrdinalIgnoreCase);
        var standup = summary.Sections.First(s => s.Id == "meetings").Items.First();
        Assert.False(string.IsNullOrWhiteSpace(standup.Organizer));
        Assert.NotEmpty(standup.Attendees ?? []);
        Assert.False(string.IsNullOrWhiteSpace(standup.TeamsLink));
    }

    [Fact]
    public async Task DisabledSourceIsSkipped()
    {
        var aggregator = DayTestHarness.Aggregator(new DisabledSource(), new MockDayDataSource());
        var summary = await aggregator.GetAsync(
            DayTestHarness.Query(new WorkspaceActor("dev-you", "You", null)),
            CancellationToken.None);

        Assert.DoesNotContain("disabled", summary.Sources);
        Assert.Contains("mock", summary.Sources);
    }

    private sealed class DisabledSource : IDayDataSource
    {
        public string Name => "disabled";
        public bool IsEnabled => false;

        public Task<IReadOnlyList<DaySection>> GetAsync(DayQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DaySection>>([new DaySection("meetings", "Nope", [])]);
    }
}

internal static class DayTestHarness
{
    public static DayQuery Query(
        WorkspaceActor? actor = null,
        DateOnly? date = null,
        string timeZone = "UTC",
        DateTimeOffset? now = null)
    {
        var day = date ?? new DateOnly(2026, 11, 4);
        var tz = DayTimeZones.Resolve(timeZone);
        return new DayQuery(
            actor ?? new WorkspaceActor("dev-you", "You", "you@etc.local"),
            day,
            DayTimeZones.Normalize(timeZone),
            now ?? DayTimeZones.At(day, 8, 15, tz));
    }

    public static DayAggregator Aggregator(
        params IDayDataSource[] sources) =>
        Aggregator(new DisabledCalendar(), sources);

    public static DayAggregator Aggregator(
        IUserGraphCalendarClient calendar,
        params IDayDataSource[] sources) =>
        Aggregator(calendar, new MemoryCache(new MemoryCacheOptions()), sources);

    public static DayAggregator Aggregator(
        IUserGraphCalendarClient calendar,
        IMemoryCache cache,
        params IDayDataSource[] sources)
    {
        var list = sources.Length == 0 ? [new MockDayDataSource()] : sources;
        return new DayAggregator(list, calendar, new DeterministicOnlyBrief(), cache);
    }

    public static DayMeeting Meeting(
        string id,
        string subject,
        DateTimeOffset start,
        DateTimeOffset end,
        string? organizer = "Alex",
        IReadOnlyList<string>? attendees = null,
        string? location = null,
        string? teams = null,
        string? preview = null) =>
        new(id, subject, start, end, organizer, attendees ?? [], location, teams, preview, null);
}

internal sealed class DisabledCalendar : IUserGraphCalendarClient
{
    public bool IsEnabled => false;

    public Task<CalendarDayResult> GetTodaysMeetingsAsync(DayQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new CalendarDayResult(CalendarReadStates.Disabled, null, []));
}

internal sealed class ScriptedCalendar : IUserGraphCalendarClient
{
    public bool IsEnabled { get; set; } = true;
    public CalendarDayResult Result { get; set; } = new(CalendarReadStates.Ok, null, []);
    public int Calls { get; private set; }
    public List<string> Oids { get; } = [];

    public Task<CalendarDayResult> GetTodaysMeetingsAsync(DayQuery query, CancellationToken cancellationToken)
    {
        Calls++;
        Oids.Add(query.Actor.Oid);
        return Task.FromResult(Result);
    }
}

internal sealed class DeterministicOnlyBrief : IMeetingBriefGenerator
{
    public Task<MeetingBriefDto> GenerateAsync(
        IReadOnlyList<DayMeeting> meetings,
        DayQuery query,
        CancellationToken cancellationToken) =>
        Task.FromResult(DeterministicMeetingBrief.Build(meetings, query));
}
