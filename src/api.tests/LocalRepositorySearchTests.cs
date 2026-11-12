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
    public void TokenizeDropsStopwordsSoScopedMissesDoNotMatchEveryFile()
    {
        var terms = LocalRepositorySearch.Tokenize("I didn't find anything about that in the folder");
        Assert.DoesNotContain("the", terms);
        Assert.DoesNotContain("that", terms);
        Assert.DoesNotContain("about", terms);
        Assert.DoesNotContain("find", terms);
        Assert.Contains("folder", terms);
    }

    [Fact]
    public void ScoreDoesNotCountSubstringHitsInsideOtherWords()
    {
        Assert.Equal(0, LocalRepositorySearch.Score("Meeting notes for planning", ["in"]));
        Assert.True(LocalRepositorySearch.Score("wifi password is posted", ["wifi", "password"]) >= 2);
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

    [Fact]
    public void JoinOverlappingChunksStripsTheOverlapWindow()
    {
        var joined = LocalRepositorySearch.JoinOverlappingChunks(
            ["alpha duplicated-bullet omega", "duplicated-bullet omega tail"],
            overlap: 32);
        var first = joined.IndexOf("duplicated-bullet", StringComparison.Ordinal);
        var last = joined.LastIndexOf("duplicated-bullet", StringComparison.Ordinal);
        Assert.Equal(first, last);
        Assert.Contains("alpha", joined, StringComparison.Ordinal);
        Assert.Contains("tail", joined, StringComparison.Ordinal);
    }
}
