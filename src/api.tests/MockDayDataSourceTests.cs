using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Day;
using Xunit;

namespace Intranet.Api.Tests;

public class MockDayDataSourceTests
{
    [Fact]
    public async Task AggregatorMergesMockSectionsInWireframeOrder()
    {
        var aggregator = new DayAggregator([new MockDayDataSource()]);
        var actor = new WorkspaceActor("dev-you", "You", "you@etc.local");
        var summary = await aggregator.GetAsync(actor, new DateOnly(2026, 11, 4), CancellationToken.None);

        Assert.Equal(["meetings", "tasks", "follow-ups", "suggestions"], summary.Sections.Select(s => s.Id).ToArray());
        Assert.Contains("mock", summary.Sources);
        Assert.Equal("You", summary.UserDisplayName);
        Assert.True(summary.Sections.First(s => s.Id == "meetings").Items.Count >= 3);
        Assert.True(summary.Sections.First(s => s.Id == "tasks").Items.Count >= 5);
    }

    [Fact]
    public async Task DisabledSourceIsSkipped()
    {
        var aggregator = new DayAggregator([new DisabledSource(), new MockDayDataSource()]);
        var summary = await aggregator.GetAsync(
            new WorkspaceActor("dev-you", "You", null),
            DateOnly.FromDateTime(DateTime.Today),
            CancellationToken.None);

        Assert.DoesNotContain("disabled", summary.Sources);
        Assert.Contains("mock", summary.Sources);
    }

    private sealed class DisabledSource : IDayDataSource
    {
        public string Name => "disabled";
        public bool IsEnabled => false;

        public Task<IReadOnlyList<DaySection>> GetAsync(
            WorkspaceActor actor,
            DateOnly date,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DaySection>>([new DaySection("meetings", "Nope", [])]);
    }
}
