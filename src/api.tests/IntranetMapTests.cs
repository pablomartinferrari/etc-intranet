using System.Text.Json;
using Intranet.Api.Help;
using Xunit;

namespace Intranet.Api.Tests;

public class IntranetMapTests
{
    [Theory]
    [InlineData("where is chat?", "chat")]
    [InlineData("Where do I go to create a chat?", "chat")]
    [InlineData("how do I request a feature?", "requests")]
    [InlineData("what do planned and done mean on the queue?", "requests")]
    [InlineData("lead inspection xrf", "lead")]
    [InlineData("how do I sign in with Microsoft?", "signin")]
    [InlineData("what is this Help panel?", "help")]
    [InlineData("where is the sales hub?", "sales")]
    [InlineData("add knowledge", "agent-sources")]
    [InlineData("connect sharepoint", "agent-sources")]
    [InlineData("add sharepoint folder", "chat")]
    public void RankPutsExpectedPlaceFirst(string question, string placeId)
    {
        var ranked = IntranetMap.Rank(question);
        Assert.NotEmpty(ranked);
        Assert.Equal(placeId, ranked[0].PlaceId);
        Assert.True(ranked[0].Score >= IntranetMap.MinMatchScore);
    }

    [Fact]
    public void RankDiffersByQuestion()
    {
        var chat = IntranetMap.Rank("where is chat?");
        var requests = IntranetMap.Rank("how do I request a feature?");
        var bids = IntranetMap.Rank("where are bids?");

        Assert.Equal("chat", chat[0].PlaceId);
        Assert.Equal("requests", requests[0].PlaceId);
        Assert.Equal("bids", bids[0].PlaceId);
        Assert.NotEqual(chat[0].PlaceId, requests[0].PlaceId);
        Assert.NotEqual(chat[0].PlaceId, bids[0].PlaceId);
    }

    [Fact]
    public void UnknownQuestionDoesNotScoreAPlace()
    {
        Assert.Empty(IntranetMap.Rank("what color is the cafeteria?"));
        var mapped = IntranetMap.Match("what color is the cafeteria?");
        Assert.Contains("four Home apps", mapped.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestsPlaceDocumentsQueueStatuses()
    {
        var requests = Assert.Single(IntranetMap.Places, p => p.Id == "requests");
        Assert.Contains("planned", requests.Purpose, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("done", requests.Purpose, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(requests.CommonQuestions, q => q.Contains("planned", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("/sales/requests", requests.Paths);
    }

    [Fact]
    public void PromptJsonIncludesRichMetadata()
    {
        Assert.Contains("howToGetThere", IntranetMap.PromptJson, StringComparison.Ordinal);
        Assert.Contains("commonQuestions", IntranetMap.PromptJson, StringComparison.Ordinal);
        Assert.Contains("dataSources", IntranetMap.PromptJson, StringComparison.Ordinal);
        Assert.Contains("/knowledge", IntranetMap.PromptJson, StringComparison.Ordinal);
        Assert.Contains("Entra", IntranetMap.PromptJson, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendCatalogFileMatchesBackend()
    {
        var path = FindRepoFile(Path.Combine("src", "web", "src", "help", "intranet-map.json"));
        var expected = NormalizeJson(IntranetMap.FrontendCatalogJson);
        if (string.Equals(Environment.GetEnvironmentVariable("DUMP_HELP_MAP"), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(path, expected + Environment.NewLine);
        }

        Assert.True(File.Exists(path), $"Missing {path}. Run tests with DUMP_HELP_MAP=1 to write it.");
        var actual = NormalizeJson(File.ReadAllText(path));
        Assert.Equal(expected, actual);

        var catalog = JsonSerializer.Deserialize<HelpFrontendCatalog>(actual, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        Assert.NotNull(catalog);
        Assert.Equal(IntranetMap.SuggestedQuestions, catalog!.SuggestedQuestions);
        Assert.Equal(IntranetMap.Places.Count, catalog.Places.Count);
    }

    private static string NormalizeJson(string json) =>
        json.Replace("\r\n", "\n").TrimEnd();

    private static string FindRepoFile(string relativeFromRepoRoot)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "api", "Intranet.Api.csproj")))
            {
                return Path.Combine(dir.FullName, relativeFromRepoRoot);
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repo file {relativeFromRepoRoot}.");
    }
}
