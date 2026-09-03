using Intranet.Api.Help;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intranet.Api.Tests;

public class HelpAskServiceTests
{
    [Theory]
    [InlineData("Where do I go to create a chat?", "/knowledge", "Chat")]
    [InlineData("new chat", "/knowledge", "Chat")]
    [InlineData("Where are bids?", "/opportunities", "Bids")]
    [InlineData("How do I request a feature?", "/requests", "Feature Requests")]
    [InlineData("request a change", "/requests", "Feature Requests")]
    [InlineData("lead inspection", "/lead-inspection", "Lead")]
    [InlineData("add knowledge", "/knowledge/sources", "Agent sources")]
    [InlineData("connect sharepoint", "/knowledge/sources", "Agent sources")]
    public async Task MapAnswersKnownNavigationQuestions(string question, string path, string label)
    {
        var service = CreateService(new NullLlm());

        var result = await service.AskAsync(question, CancellationToken.None);

        Assert.Equal(HelpAskService.SourceMap, result.Source);
        Assert.Contains(result.Links, link => link.Path == path && link.Label == label);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
    }

    [Fact]
    public async Task PipelineVsBidsReturnsBothSalesApps()
    {
        var result = await CreateService(new NullLlm())
            .AskAsync("What's Pipeline vs Bids?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceMap, result.Source);
        Assert.Contains(result.Links, link => link.Path == "/opportunities");
        Assert.Contains(result.Links, link => link.Path == "/pipeline");
        Assert.Contains("Bids", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pipeline", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/knowledge", result.Links.Select(l => l.Path));
    }

    [Fact]
    public async Task UnknownQuestionStillReturnsHomeOverview()
    {
        var result = await CreateService(new NullLlm())
            .AskAsync("What color is the cafeteria?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceMap, result.Source);
        Assert.Contains(result.Links, link => link.Path == "/");
        Assert.Contains("Chat", result.Answer, StringComparison.Ordinal);
        Assert.Contains("Lead", result.Answer, StringComparison.Ordinal);
        Assert.Contains("Sales", result.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyQuestionIsRejected()
    {
        var service = CreateService(new NullLlm());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AskAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task LlmJsonIsUsedWhenValid()
    {
        var llm = new ScriptedLlm(
            """
            {
              "answer": "Open the Chat card on Home, then New project and New chat.",
              "placeIds": ["chat"]
            }
            """);
        var result = await CreateService(llm).AskAsync("create a chat", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceLlm, result.Source);
        Assert.Equal("Open the Chat card on Home, then New project and New chat.", result.Answer);
        Assert.Equal("/knowledge", Assert.Single(result.Links).Path);
        Assert.Contains("intranet map", llm.LastUserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create a chat", llm.LastUserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InventedPlaceIdsAreIgnored()
    {
        var llm = new ScriptedLlm(
            """
            { "answer": "Try the secret HR portal.", "placeIds": ["hr-portal", "chat"] }
            """);

        var result = await CreateService(llm).AskAsync("where is chat?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceLlm, result.Source);
        Assert.DoesNotContain(result.Links, link => link.Path.Contains("hr", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Links, link => link.Path == "/knowledge");
    }

    [Fact]
    public async Task LlmGarbageFallsBackToMap()
    {
        var result = await CreateService(new ScriptedLlm("not json at all"))
            .AskAsync("Where are bids?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceMap, result.Source);
        Assert.Contains(result.Links, link => link.Path == "/opportunities");
    }

    [Fact]
    public async Task LlmNullFallsBackToMap()
    {
        var result = await CreateService(new NullLlm())
            .AskAsync("Where are bids?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceMap, result.Source);
        Assert.Contains(result.Links, link => link.Path == "/opportunities");
    }

    [Fact]
    public void MapDoesNotInventApps()
    {
        var paths = IntranetMap.Places.Select(p => p.Path).ToHashSet(StringComparer.Ordinal);
        Assert.True(paths.SetEquals([
            "/",
            "/knowledge",
            "/lead-inspection",
            "/sales",
            "/opportunities",
            "/pipeline",
            "/requests",
            "/knowledge/sources",
        ]));
    }

    [Fact]
    public void SuggestedQuestionsAreTheFourStarters()
    {
        Assert.Equal(4, IntranetMap.SuggestedQuestions.Count);
        Assert.Contains(IntranetMap.SuggestedQuestions, q => q.Contains("chat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(IntranetMap.SuggestedQuestions, q => q.Contains("bids", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(IntranetMap.SuggestedQuestions, q => q.Contains("feature", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(IntranetMap.SuggestedQuestions, q => q.Contains("Pipeline", StringComparison.Ordinal));
    }

    private static HelpAskService CreateService(IHelpLlm llm) =>
        new(llm, NullLogger<HelpAskService>.Instance);

    private sealed class NullLlm : IHelpLlm
    {
        public Task<string?> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class ScriptedLlm(string? reply) : IHelpLlm
    {
        public string LastUserPrompt { get; private set; } = string.Empty;

        public Task<string?> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            LastUserPrompt = userPrompt;
            return Task.FromResult(reply);
        }
    }
}
