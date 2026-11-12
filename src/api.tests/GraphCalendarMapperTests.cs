using Intranet.Api.Agentic.Day;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Xunit;

namespace Intranet.Api.Tests;

public class GraphCalendarMapperTests
{
    [Fact]
    public void MapsSubjectTimeOrganizerAttendeesLocationTeamsAndPreview()
    {
        var ev = new Event
        {
            Id = "evt-1",
            Subject = "Project Alpha review",
            Start = new DateTimeTimeZone { DateTime = "2026-11-04T11:00:00", TimeZone = "UTC" },
            End = new DateTimeTimeZone { DateTime = "2026-11-04T12:00:00", TimeZone = "UTC" },
            Organizer = new Recipient { EmailAddress = new EmailAddress { Name = "Pat Lee", Address = "pat@etc.local" } },
            Attendees =
            [
                new Attendee { EmailAddress = new EmailAddress { Name = "Pat Lee", Address = "pat@etc.local" } },
                new Attendee { EmailAddress = new EmailAddress { Name = "User B", Address = "b@etc.local" } },
                new Attendee { EmailAddress = new EmailAddress { Name = "User C", Address = "c@etc.local" } },
            ],
            Location = new Location { DisplayName = "Conference A" },
            OnlineMeeting = new OnlineMeetingInfo { JoinUrl = "https://teams.microsoft.com/l/meetup-join/abc" },
            BodyPreview = "Walk the quarterly report draft.",
            WebLink = "https://outlook.office.com/item/evt-1",
            IsOnlineMeeting = true,
        };

        var meeting = GraphCalendarMapper.ToMeeting(ev);

        Assert.NotNull(meeting);
        Assert.Equal("Project Alpha review", meeting!.Subject);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 11, 0, 0, TimeSpan.Zero), meeting.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero), meeting.End);
        Assert.Equal("Pat Lee", meeting.Organizer);
        Assert.Equal(["User B", "User C"], meeting.Attendees);
        Assert.Equal("Conference A", meeting.Location);
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/abc", meeting.TeamsLink);
        Assert.Equal("Walk the quarterly report draft.", meeting.BodyPreview);
        Assert.Equal("https://outlook.office.com/item/evt-1", meeting.WebLink);
    }

    [Fact]
    public void SkipsCancelledAndUsesOnlineMeetingUrl()
    {
        Assert.Null(GraphCalendarMapper.ToMeeting(new Event { Id = "x", IsCancelled = true, Subject = "Nope" }));

        var teamsOnly = GraphCalendarMapper.ToMeeting(new Event
        {
            Id = "online",
            Subject = "Sync",
            IsOnlineMeeting = true,
            OnlineMeetingUrl = "https://teams.microsoft.com/l/meetup-join/fallback",
        });
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/fallback", teamsOnly!.TeamsLink);
        Assert.Equal("Microsoft Teams", teamsOnly.Location);
    }

    [Fact]
    public void ConsentDetectionDoesNotTreatGenericFailuresAsConsent()
    {
        Assert.True(GraphCalendarMapper.IsConsentError(new CalendarConsentException()));
        Assert.True(GraphCalendarMapper.IsConsentError(new MsalUiRequiredException("consent_required", "AADSTS65001 interaction required")));
        Assert.True(GraphCalendarMapper.IsConsentError(new InvalidOperationException("The user or administrator has not consented to Calendars.Read")));
        Assert.False(GraphCalendarMapper.IsConsentError(new HttpRequestException("connection reset")));
    }

    [Fact]
    public void TimeZoneNormalizeRejectsJunkAndKeepsIana()
    {
        Assert.Equal("UTC", DayTimeZones.Normalize(null));
        Assert.Equal("UTC", DayTimeZones.Normalize("America/New_York\nAuthorization: Bearer x"));
        Assert.Equal("America/New_York", DayTimeZones.Normalize("America/New_York"));
        Assert.Equal("UTC", DayTimeZones.Normalize("Not/AZone"));
    }

    [Fact]
    public void GraphClientNeverExposesUsersPathHelper()
    {
        // Guardrail: the production client must call /me/calendarView, not /users/{oid}.
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "api", "Agentic", "Day", "GraphCalendar.cs"));
        Assert.Contains("client.Me.CalendarView", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Users[", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Users.", source, StringComparison.Ordinal);
    }
}
