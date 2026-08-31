using System.Text.Json;
using Intranet.Api.Cleat;
using Xunit;

namespace Intranet.Api.Tests;

public class CleatJsonMapperTests
{
    [Fact]
    public void MapsZapierNestedContractAndScore()
    {
        var json = """
            {
              "items": [
                {
                  "id": "rec_1",
                  "match_score": 91.5,
                  "match_reason": "NAICS and past performance align",
                  "in_pipeline": false,
                  "contract": {
                    "id": "contract_abc",
                    "title": "Environmental sampling",
                    "agencyName": "U.S. Army Corps of Engineers",
                    "naicsId": "541620",
                    "postedDate": "2026-08-01T00:00:00Z",
                    "deadlineDate": "2026-09-15T21:00:00Z",
                    "solicitationNumber": "W912P626R0001",
                    "typeOfSetAsideDescription": "Total Small Business",
                    "summary": "Lab analysis support",
                    "overview": "Field and lab testing",
                    "sourceUrl": "https://sam.gov/opp/example"
                  }
                }
              ],
              "has_more": true,
              "next_cursor": "cur_2"
            }
            """;

        var mapped = CleatJsonMapper.MapRecommendations(Parse(json), "https://www.cleat.ai");

        Assert.True(mapped.HasMore);
        Assert.Equal("cur_2", mapped.NextCursor);
        var item = Assert.Single(mapped.Items);
        Assert.Equal("contract_abc", item.Id);
        Assert.Equal("Environmental sampling", item.Title);
        Assert.Equal("U.S. Army Corps of Engineers", item.Agency);
        Assert.Equal("541620", item.Naics);
        Assert.Equal(91.5, item.Score);
        Assert.Equal("W912P626R0001", item.SolicitationNumber);
        Assert.Equal("Total Small Business", item.SetAside);
        Assert.Equal("Lab analysis support", item.Summary);
        Assert.Equal("Field and lab testing", item.Overview);
        Assert.False(item.InPipeline);
        Assert.Equal("NAICS and past performance align", item.MatchReason);
        Assert.Equal("https://sam.gov/opp/example", item.SourceUrl);
        Assert.Equal("https://www.cleat.ai/dashboard/contracts/contract_abc", item.CleatusUrl);
    }

    [Fact]
    public void PrefersNestedContractIdForGetById()
    {
        var json = """
            {
              "id": "rec_parent",
              "match_score": 88,
              "contract": { "id": "contract_nested", "title": "Nested" }
            }
            """;

        var mapped = CleatJsonMapper.TryMapOpportunity(Parse(json), "https://www.cleat.ai");
        Assert.NotNull(mapped);
        Assert.Equal("contract_nested", mapped.Id);
        Assert.Equal("Nested", mapped.Title);
        Assert.Equal(88, mapped.Score);
    }

    [Fact]
    public void MapsCamelCaseFlatOpportunity()
    {
        var json = """
            {
              "id": "forecast_9",
              "title": "Lab hood recertification",
              "agencyName": "EPA",
              "naicsId": "541380",
              "matchScore": 80,
              "postedDate": "2026-07-04",
              "deadlineDate": "2026-08-20",
              "solicitationNumber": "68HERH26R0002",
              "typeOfSetAside": "WOSB",
              "cleatusUrl": "https://www.cleat.ai/dashboard/contracts/forecast_9"
            }
            """;

        var mapped = CleatJsonMapper.TryMapOpportunity(Parse(json), "https://example.invalid");
        Assert.NotNull(mapped);
        Assert.Equal("Lab hood recertification", mapped.Title);
        Assert.Equal("EPA", mapped.Agency);
        Assert.Equal(80, mapped.Score);
        Assert.Equal("WOSB", mapped.SetAside);
        Assert.Equal("https://www.cleat.ai/dashboard/contracts/forecast_9", mapped.CleatusUrl);
    }

    [Fact]
    public void SkipsItemsWithoutIdAndDoesNotThrowOnUnknownFields()
    {
        var json = """
            {
              "recommendations": [
                { "title": "no id here", "extra": { "nested": true } },
                { "id": "contract_ok", "title": "Keep me", "unexpectedArray": [1, 2, 3] }
              ]
            }
            """;

        var mapped = CleatJsonMapper.MapRecommendations(Parse(json), "https://www.cleat.ai");
        var item = Assert.Single(mapped.Items);
        Assert.Equal("contract_ok", item.Id);
        Assert.Equal("Keep me", item.Title);
    }

    [Fact]
    public void RootArrayIsAccepted()
    {
        var json = """
            [
              { "id": "contract_a", "title": "A" },
              { "id": "contract_b", "title": "B" }
            ]
            """;

        var mapped = CleatJsonMapper.MapRecommendations(Parse(json), "https://www.cleat.ai");
        Assert.Equal(2, mapped.Items.Count);
        Assert.False(mapped.HasMore);
    }

    [Fact]
    public void MissingOptionalFieldsStayNull()
    {
        var mapped = CleatJsonMapper.TryMapOpportunity(Parse("""{ "id": "contract_min" }"""), "https://www.cleat.ai");
        Assert.NotNull(mapped);
        Assert.Null(mapped.Title);
        Assert.Null(mapped.Agency);
        Assert.Null(mapped.Score);
        Assert.Null(mapped.Summary);
        Assert.Equal("https://www.cleat.ai/dashboard/contracts/contract_min", mapped.CleatusUrl);
    }

    [Fact]
    public void IgnoresNonCleatusUrlFieldsWhenBuildingDeepLink()
    {
        var json = """
            { "id": "contract_x", "url": "https://sam.gov/not-cleatus", "sourceUrl": "https://sam.gov/opp/1" }
            """;
        var mapped = CleatJsonMapper.TryMapOpportunity(Parse(json), "https://www.cleat.ai");
        Assert.NotNull(mapped);
        Assert.Equal("https://www.cleat.ai/dashboard/contracts/contract_x", mapped.CleatusUrl);
        Assert.Equal("https://sam.gov/opp/1", mapped.SourceUrl);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
