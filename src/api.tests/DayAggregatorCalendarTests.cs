using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Day;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Intranet.Api.Tests;

public class DayAggregatorCalendarTests
{
    [Fact]
    public async Task SuccessfulGraphDayReplacesMockMeetingsEvenWhenEmpty()
    {
        var calendar = new ScriptedCalendar
        {
            Result = new CalendarDayResult(CalendarReadStates.Ok, null, []),
        };
        var aggregator = DayTestHarness.Aggregator(calendar, new MockDayDataSource());
        var actor = new WorkspaceActor("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "Pat", "pat@etc.local");

        var summary = await aggregator.GetAsync(DayTestHarness.Query(actor), CancellationToken.None);

        Assert.Equal(CalendarReadStates.Ok, summary.Calendar.State);
        Assert.Contains("graph-calendar", summary.Sources);
        Assert.Empty(summary.Sections.First(s => s.Id == "meetings").Items);
        Assert.True(summary.Sections.First(s => s.Id == "tasks").Items.Count >= 5);
        Assert.Equal("No meetings on the calendar today. You have the day open.", summary.Brief?.Text);
    }

    [Fact]
    public async Task SuccessfulGraphMeetingsAreUserScopedAndKeepOrganizerAttendeesTeams()
    {
        var query = DayTestHarness.Query(new WorkspaceActor("oid-pat", "Pat", "pat@etc.local"));
        var calendar = new ScriptedCalendar
        {
            Result = new CalendarDayResult(
                CalendarReadStates.Ok,
                null,
                [
                    DayTestHarness.Meeting(
                        "g1",
                        "Budget review",
                        DayTimeZones.At(query.Date, 10, 0, TimeZoneInfo.Utc),
                        DayTimeZones.At(query.Date, 11, 0, TimeZoneInfo.Utc),
                        organizer: "Pat",
                        attendees: ["User B"],
                        location: "Microsoft Teams",
                        teams: "https://teams.microsoft.com/l/meetup-join/real",
                        preview: "Bring last quarter numbers."),
                ]),
        };
        var aggregator = DayTestHarness.Aggregator(calendar, new MockDayDataSource());

        var summary = await aggregator.GetAsync(query, CancellationToken.None);
        var meeting = Assert.Single(summary.Sections.First(s => s.Id == "meetings").Items);

        Assert.Equal("Budget review", meeting.Title);
        Assert.Equal("Pat", meeting.Organizer);
        Assert.Equal(["User B"], meeting.Attendees);
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/real", meeting.TeamsLink);
        Assert.Equal("Bring last quarter numbers.", meeting.BodyPreview);
        Assert.DoesNotContain(summary.Sections.First(s => s.Id == "meetings").Items, i => i.Id == "m1");
        Assert.Equal(["oid-pat"], calendar.Oids);
    }

    [Fact]
    public async Task ConsentHidesMockMeetingsAndSurfacesMessage()
    {
        var calendar = new ScriptedCalendar
        {
            Result = new CalendarDayResult(
                CalendarReadStates.ConsentRequired,
                CalendarConsentException.UserMessage,
                []),
        };
        var aggregator = DayTestHarness.Aggregator(calendar, new MockDayDataSource());
        var actor = new WorkspaceActor("oid-pat", "Pat", "pat@etc.local");

        var summary = await aggregator.GetAsync(DayTestHarness.Query(actor), CancellationToken.None);

        Assert.Equal(CalendarReadStates.ConsentRequired, summary.Calendar.State);
        Assert.Contains("Calendars.Read", summary.Calendar.Message, StringComparison.Ordinal);
        Assert.Empty(summary.Sections.First(s => s.Id == "meetings").Items);
        Assert.DoesNotContain("Pipeline standup", summary.Sections.SelectMany(s => s.Items).Select(i => i.Title));
    }

    [Fact]
    public async Task DemoIdentityKeepsMockAndNeverCallsGraph()
    {
        var calendar = new ScriptedCalendar();
        var aggregator = DayTestHarness.Aggregator(calendar, new MockDayDataSource());

        var summary = await aggregator.GetAsync(DayTestHarness.Query(), CancellationToken.None);

        Assert.Equal(0, calendar.Calls);
        Assert.Equal(CalendarReadStates.Mock, summary.Calendar.State);
        Assert.Contains("Pipeline standup", summary.Sections.SelectMany(s => s.Items).Select(i => i.Title));
    }

    [Fact]
    public async Task CacheIsScopedPerUser()
    {
        var queryA = DayTestHarness.Query(new WorkspaceActor("oid-a", "Ada", null));
        var queryB = DayTestHarness.Query(new WorkspaceActor("oid-b", "Bea", null));
        var calendar = new ScriptedCalendar
        {
            Result = new CalendarDayResult(
                CalendarReadStates.Ok,
                null,
                [
                    DayTestHarness.Meeting(
                        "only-a",
                        "Ada private 1:1",
                        DayTimeZones.At(queryA.Date, 13, 0, TimeZoneInfo.Utc),
                        DayTimeZones.At(queryA.Date, 13, 30, TimeZoneInfo.Utc)),
                ]),
        };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var aggregator = DayTestHarness.Aggregator(calendar, cache, new MockDayDataSource());

        var first = await aggregator.GetAsync(queryA, CancellationToken.None);
        calendar.Result = new CalendarDayResult(
            CalendarReadStates.Ok,
            null,
            [
                DayTestHarness.Meeting(
                    "only-b",
                    "Bea standup",
                    DayTimeZones.At(queryB.Date, 9, 0, TimeZoneInfo.Utc),
                    DayTimeZones.At(queryB.Date, 9, 15, TimeZoneInfo.Utc)),
            ]);
        var second = await aggregator.GetAsync(queryB, CancellationToken.None);
        var firstAgain = await aggregator.GetAsync(queryA, CancellationToken.None);

        Assert.Contains("Ada private 1:1", first.Sections.SelectMany(s => s.Items).Select(i => i.Title));
        Assert.Contains("Bea standup", second.Sections.SelectMany(s => s.Items).Select(i => i.Title));
        Assert.DoesNotContain("Bea standup", firstAgain.Sections.SelectMany(s => s.Items).Select(i => i.Title));
        Assert.DoesNotContain("Ada private 1:1", second.Sections.SelectMany(s => s.Items).Select(i => i.Title));
        Assert.Equal(2, calendar.Calls);
        Assert.Equal("agentic:day:oid-a:2026-11-04:UTC", DayAggregator.CacheKey(queryA));
        Assert.Equal("agentic:day:oid-b:2026-11-04:UTC", DayAggregator.CacheKey(queryB));
    }
}
