using Intranet.Api.Agentic.Repository;
using Xunit;

namespace Intranet.Api.Tests;

public class LocalRepositorySearchTests
{
    [Fact]
    public void TokenizeDropsShortAndDuplicateTerms()
    {
        var terms = LocalRepositorySearch.Tokenize("Quarterly report, quarterly REPORT!");
        Assert.Equal(["quarterly", "report"], terms);
    }

    [Fact]
    public void ScoreCountsOverlaps()
    {
        var score = LocalRepositorySearch.Score(
            "The quarterly report mentions the quarterly report twice.",
            ["quarterly", "report"]);
        Assert.True(score >= 4);
    }

    [Fact]
    public void HighlightSnippetKeepsContextAroundFirstHit()
    {
        var text = new string('x', 80) + " quarterly report " + new string('y', 80);
        var snippet = LocalRepositorySearch.HighlightSnippet(text, ["quarterly"]);
        Assert.Contains("quarterly", snippet, StringComparison.OrdinalIgnoreCase);
        Assert.True(snippet.Length < text.Length);
    }

    [Fact]
    public void ChunkSplitsLongText()
    {
        var chunks = LocalRepositorySearch.Chunk(new string('a', 1200), 500);
        Assert.Equal(3, chunks.Count);
        Assert.Equal(500, chunks[0].Length);
    }
}
