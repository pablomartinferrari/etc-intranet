using System.Text.Json;
using Intranet.Api.Cleat;
using Xunit;

namespace Intranet.Api.Tests;

public class PipelineMapperAndRulesTests
{
    [Fact]
    public void MapsZapierNestedPursuitWithoutTreatingLastFetchAsActivity()
    {
        var json = """
            {
              "items": [
                {
                  "id": "pur_abc",
                  "phase": "triage",
                  "columnTitle": "Triage",
                  "pursuitTitle": "Lab sampling IDIQ",
                  "createdAt": "2026-07-01T00:00:00Z",
                  "lastFetch": "2026-08-20T00:00:00Z",
                  "contract": {
                    "id": "contract_xyz",
                    "title": "Nested title",
                    "agencyName": "EPA",
                    "deadlineDate": "2026-09-01T00:00:00Z",
                    "solicitationNumber": "68HERH26R0001"
                  }
                }
              ],
              "has_more": false
            }
            """;

        var mapped = CleatJsonMapper.MapPipelineSearch(Parse(json), "https://www.cleat.ai");
        var item = Assert.Single(mapped.Items);
        Assert.Equal("pur_abc", item.Id);
        Assert.Equal("contract_xyz", item.OpportunityId);
        Assert.Equal("Lab sampling IDIQ", item.Title);
        Assert.Equal("EPA", item.Agency);
        Assert.Equal("triage", item.Phase);
        Assert.Equal("2026-09-01T00:00:00Z", item.DeadlineDate);
        Assert.False(item.LastActivityAvailable);
        Assert.Null(item.LastActivityAt);
        Assert.Null(item.Assignee);
        Assert.Equal("https://www.cleat.ai/dashboard/contracts/contract_xyz", item.CleatusUrl);
    }

    [Fact]
    public void MapsUpdatedAtAsLastActivityWhenPresent()
    {
        var mapped = CleatJsonMapper.TryMapPursuit(
            Parse("""{ "id": "pur_1", "phase": "preparing", "updated_at": "2026-08-01T12:00:00Z" }"""),
            "https://www.cleat.ai");
        Assert.NotNull(mapped);
        Assert.True(mapped.LastActivityAvailable);
        Assert.Equal("2026-08-01T12:00:00Z", mapped.LastActivityAt);
    }

    [Fact]
    public void DeadlinePassedNeedsCloseOut()
    {
        var pursuit = new PursuitDto
        {
            Id = "pur_1",
            Phase = "submitted",
            DeadlineDate = "2026-01-01T00:00:00Z",
        };
        var (needs, reasons) = PipelineCloseoutRules.Evaluate(pursuit, DateTimeOffset.Parse("2026-08-31T00:00:00Z"));
        Assert.True(needs);
        Assert.Contains("deadline_passed", reasons);
    }

    [Fact]
    public void StaleTwentyOneDaysInTriageWhenLastActivityExists()
    {
        var pursuit = new PursuitDto
        {
            Id = "pur_1",
            Phase = "triage",
            DeadlineDate = "2026-12-01T00:00:00Z",
            LastActivityAt = "2026-07-01T00:00:00Z",
            LastActivityAvailable = true,
        };
        var (needs, reasons) = PipelineCloseoutRules.Evaluate(pursuit, DateTimeOffset.Parse("2026-08-31T00:00:00Z"));
        Assert.True(needs);
        Assert.Contains("stale_21_days", reasons);
        Assert.DoesNotContain("deadline_passed", reasons);
    }

    [Fact]
    public void NoDeadlineAndNoLastActivityIsFlaggedSoItDoesNotHide()
    {
        var pursuit = new PursuitDto { Id = "pur_1", Phase = "preparing" };
        var (needs, reasons) = PipelineCloseoutRules.Evaluate(pursuit, DateTimeOffset.UtcNow);
        Assert.True(needs);
        Assert.Equal("no_deadline_on_file", Assert.Single(reasons));
    }

    [Fact]
    public void FutureDeadlineWithoutLastActivityIsNotOverdue()
    {
        var pursuit = new PursuitDto
        {
            Id = "pur_1",
            Phase = "triage",
            DeadlineDate = "2027-01-01T00:00:00Z",
        };
        var (needs, _) = PipelineCloseoutRules.Evaluate(pursuit, DateTimeOffset.Parse("2026-08-31T00:00:00Z"));
        Assert.False(needs);
    }

    [Theory]
    [InlineData("  Won  ", "won")]
    [InlineData("In Progress", "in_progress")]
    [InlineData("TRIAGE", "triage")]
    [InlineData("Preparing", "preparing")]
    public void NormalizeTrimsLowersAndTurnsSpacesIntoUnderscores(string input, string expected)
    {
        Assert.Equal(expected, PipelineCloseoutRules.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeBlankIsNull(string? input)
    {
        Assert.Null(PipelineCloseoutRules.Normalize(input));
    }

    [Fact]
    public void WonLostArchivedAreNotCloseOut()
    {
        var now = DateTimeOffset.Parse("2026-08-31T00:00:00Z");
        Assert.False(PipelineCloseoutRules.Evaluate(
            new PursuitDto { Id = "a", Phase = "won", DeadlineDate = "2020-01-01T00:00:00Z" }, now).NeedsCloseOut);
        Assert.False(PipelineCloseoutRules.Evaluate(
            new PursuitDto { Id = "b", Phase = "lost" }, now).NeedsCloseOut);
        Assert.False(PipelineCloseoutRules.Evaluate(
            new PursuitDto { Id = "c", Phase = "triage", Archived = true }, now).NeedsCloseOut);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
