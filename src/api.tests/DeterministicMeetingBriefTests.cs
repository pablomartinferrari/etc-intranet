using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Day;
using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intranet.Api.Tests;

public class DeterministicMeetingBriefTests
{
    [Fact]
    public void EmptyDayIsOpen()
    {
        var brief = DeterministicMeetingBrief.Build([], DayTestHarness.Query());
        Assert.Equal(DeterministicMeetingBrief.Source, brief.Source);
        Assert.Contains("open", brief.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Open day", brief.Highlights);
    }

    [Fact]
    public void FlagsUpcomingConflictsBackToBackPrepAndFreeBlocks()
    {
        var query = DayTestHarness.Query(now: new DateTimeOffset(2026, 11, 4, 8, 15, 0, TimeSpan.Zero));
        var tz = TimeZoneInfo.Utc;
        var meetings = new[]
        {
            DayTestHarness.Meeting("1", "Standup", At(9, 0), At(9, 30), location: "Teams"),
            DayTestHarness.Meeting("2", "Handoff", At(9, 30), At(10, 0)),
            DayTestHarness.Meeting(
                "3",
                "Project review",
                At(11, 0),
                At(12, 0),
                attendees: ["A", "B", "C", "D"],
                preview: "Bring the deck."),
            DayTestHarness.Meeting("4", "Overlap slot", At(11, 30), At(12, 0)),
        };

        var brief = DeterministicMeetingBrief.Build(meetings, query);

        Assert.Contains("Up next is Standup", brief.Text, StringComparison.Ordinal);
        Assert.Contains("Back-to-back", brief.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Overlap", brief.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Prep needed", brief.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Open blocks", brief.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(brief.Highlights, h => h.StartsWith("Next:", StringComparison.Ordinal));
        Assert.Contains(brief.Highlights, h => h.Contains("Conflict", StringComparison.OrdinalIgnoreCase) || h.Contains("overlapping", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(brief.Highlights, h => h.Contains("Back-to-back", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(brief.Highlights, h => h.StartsWith("Prep:", StringComparison.Ordinal));
        Assert.Contains(brief.Highlights, h => h.StartsWith("Free:", StringComparison.Ordinal));

        DateTimeOffset At(int hour, int minute) => DayTimeZones.At(query.Date, hour, minute, tz);
    }

    [Fact]
    public async Task GeneratorUsesLlmWhenRouterSucceeds()
    {
        var hosted = new ScriptedBriefClient(
            "Here is your day: standup is next, then a back-to-back handoff. Prep the Alpha review. You have a free hour at 10.");
        var generator = new MeetingBriefGenerator(
            new ChatCompletionRouter(
                new ScriptedBriefClient("unused"),
                hosted,
                new DownProbe(),
                new KnowledgeBaseFallbackOptions { Enabled = true, ApiKey = "test", BaseUrl = "https://example.invalid", Model = "gpt-4o-mini" },
                NullLogger<ChatCompletionRouter>.Instance),
            NullLogger<MeetingBriefGenerator>.Instance);
        var query = DayTestHarness.Query();
        var meetings = new[]
        {
            DayTestHarness.Meeting(
                "1",
                "Standup",
                DayTimeZones.At(query.Date, 9, 0, TimeZoneInfo.Utc),
                DayTimeZones.At(query.Date, 9, 30, TimeZoneInfo.Utc)),
        };

        var brief = await generator.GenerateAsync(meetings, query, CancellationToken.None);

        Assert.Equal("llm", brief.Source);
        Assert.Contains("standup is next", brief.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, hosted.CallCount);
    }

    [Fact]
    public async Task GeneratorFallsBackWhenChatUnavailable()
    {
        var generator = new MeetingBriefGenerator(
            new ChatCompletionRouter(
                new ScriptedBriefClient("unused"),
                new ScriptedBriefClient("unused"),
                new DownProbe(),
                new KnowledgeBaseFallbackOptions(),
                NullLogger<ChatCompletionRouter>.Instance),
            NullLogger<MeetingBriefGenerator>.Instance);
        var query = DayTestHarness.Query();
        var meetings = new[]
        {
            DayTestHarness.Meeting(
                "1",
                "Standup",
                DayTimeZones.At(query.Date, 9, 0, TimeZoneInfo.Utc),
                DayTimeZones.At(query.Date, 9, 30, TimeZoneInfo.Utc)),
        };

        var brief = await generator.GenerateAsync(meetings, query, CancellationToken.None);

        Assert.Equal(DeterministicMeetingBrief.Source, brief.Source);
        Assert.Contains("Standup", brief.Text, StringComparison.Ordinal);
    }

    private sealed class DownProbe : IOllamaHealthProbe
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public void Invalidate()
        {
        }
    }

    private sealed class ScriptedBriefClient(string content) : IChatCompletionClient
    {
        public string ProviderName => "openai";
        public string ModelName => "gpt-4o-mini";
        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(content);
        }
    }
}
