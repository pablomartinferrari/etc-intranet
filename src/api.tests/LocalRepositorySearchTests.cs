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
        var chunks = LocalRepositorySearch.Chunk(new string('a', 1200), 500, 0);
        Assert.Equal(3, chunks.Count);
        Assert.Equal(500, chunks[0].Length);
    }

    [Fact]
    public void ChunkOverlapsAdjacentWindows()
    {
        var chunks = LocalRepositorySearch.Chunk(new string('a', 900) + " phone 555-0100", 500, 120);
        Assert.True(chunks.Count >= 2);
        Assert.Contains("555-0100", chunks[^1], StringComparison.Ordinal);
        Assert.True(chunks[0].Length <= 500);
    }
}
