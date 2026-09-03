using Intranet.Api.Help;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intranet.Api.Tests;

public class HelpAskServiceTests
{
    [Theory]
    [InlineData("Where do I go to create a chat?", "/knowledge", "Chat")]
    [InlineData("Where is Chat?", "/knowledge", "Chat")]
    [InlineData("new chat", "/knowledge", "Chat")]
    [InlineData("Where are bids?", "/opportunities", "Bids")]
    [InlineData("How do I request a feature?", "/requests", "Feature Requests")]
    [InlineData("request a change", "/requests", "Feature Requests")]
    [InlineData("lead inspection", "/lead-inspection", "Lead")]
    [InlineData("How do I sign in?", "/", "Sign in")]
    [InlineData("What do planned and done mean?", "/requests", "Feature Requests")]
    [InlineData("add knowledge", "/knowledge/sources", "Agent sources")]
    [InlineData("connect sharepoint", "/knowledge/sources", "Agent sources")]
    [InlineData("index this SharePoint folder", "/knowledge", "Chat")]
    [InlineData("add sharepoint folder", "/knowledge", "Chat")]
    public async Task MapAnswersKnownNavigationQuestions(string question, string path, string label)
    {
        var service = CreateService(new NullLlm());

        var result = await service.AskAsync(question, CancellationToken.None);

        Assert.Equal(HelpAskService.SourceMap, result.Source);
        Assert.Null(result.Provider);
        Assert.Null(result.Model);
        Assert.Contains(result.Links, link => link.Path == path && link.Label == label);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
    }

    [Fact]
    public async Task DistinctQuestionsProduceDistinctMapAnswers()
    {
        var service = CreateService(new NullLlm());
        var chat = await service.AskAsync("where is chat?", CancellationToken.None);
        var request = await service.AskAsync("how do I request a feature?", CancellationToken.None);
        var compare = await service.AskAsync("bids vs pipeline?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceMap, chat.Source);
        Assert.Equal(HelpAskService.SourceMap, request.Source);
        Assert.Equal(HelpAskService.SourceMap, compare.Source);
        Assert.NotEqual(chat.Answer, request.Answer);
        Assert.NotEqual(chat.Answer, compare.Answer);
        Assert.NotEqual(request.Answer, compare.Answer);
        Assert.Contains(chat.Links, link => link.Path == "/knowledge");
        Assert.Contains(request.Links, link => link.Path == "/requests");
        Assert.Contains(compare.Links, link => link.Path == "/opportunities");
        Assert.Contains(compare.Links, link => link.Path == "/pipeline");
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
        Assert.Equal("openai", result.Provider);
        Assert.Equal("gpt-4o-mini", result.Model);
        Assert.Equal("Open the Chat card on Home, then New project and New chat.", result.Answer);
        Assert.Equal("/knowledge", Assert.Single(result.Links).Path);
        Assert.Contains("intranet map", llm.LastUserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"id\":\"chat\"", llm.LastUserPrompt.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.Contains("create a chat", llm.LastUserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AiPreferredPathIsUsedEvenWhenMapWouldMatch()
    {
        var llm = new ScriptedLlm(
            """
            { "answer": "LLM-specific chat directions.", "placeIds": ["chat"] }
            """);

        var result = await CreateService(llm).AskAsync("Where is Chat?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceLlm, result.Source);
        Assert.Equal("LLM-specific chat directions.", result.Answer);
        Assert.Equal(1, llm.CallCount);
        Assert.Contains("Staff question:", llm.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("Where is Chat?", llm.LastUserPrompt, StringComparison.Ordinal);
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
    public async Task LlmGarbageFallsBackToMapForThatQuestion()
    {
        var result = await CreateService(new ScriptedLlm("not json at all"))
            .AskAsync("Where are bids?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceMap, result.Source);
        Assert.Null(result.Provider);
        Assert.Contains(result.Links, link => link.Path == "/opportunities");
        Assert.Contains("Bids", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cafeteria", result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LlmJsonWithoutAnswerDoesNotReuseMapAsLlmSource()
    {
        var parsed = HelpAskService.TryParseLlm("""{ "placeIds": ["chat"] }""");
        Assert.Null(parsed);

        var result = await CreateService(new ScriptedLlm("""{ "placeIds": ["chat"] }"""))
            .AskAsync("Where are bids?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceMap, result.Source);
        Assert.Contains(result.Links, link => link.Path == "/opportunities");
        Assert.DoesNotContain("/knowledge", result.Links.Select(l => l.Path));
    }

    [Fact]
    public void TryParseLlmReturnsNullOnGarbage()
    {
        Assert.Null(HelpAskService.TryParseLlm("not json at all"));
        Assert.Null(HelpAskService.TryParseLlm(""));
        Assert.Null(HelpAskService.TryParseLlm("""{ "answer": "" }"""));
        Assert.Null(HelpAskService.TryParseLlm("[]"));
    }

    [Fact]
    public void TryParseLlmReadsFencedJson()
    {
        var parsed = HelpAskService.TryParseLlm(
            """
            ```json
            { "answer": "Open Feature Requests on Home.", "place_ids": ["requests"] }
            ```
            """);

        Assert.NotNull(parsed);
        Assert.Equal("Open Feature Requests on Home.", parsed.Value.Answer);
        Assert.Equal("requests", Assert.Single(parsed.Value.PlaceIds));
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
    public async Task EmptyLlmPlaceIdsUseMapLinksForTheQuestion()
    {
        var llm = new ScriptedLlm(
            """
            { "answer": "Chat is the knowledge-base app on Home.", "placeIds": [] }
            """);

        var result = await CreateService(llm).AskAsync("where is chat?", CancellationToken.None);

        Assert.Equal(HelpAskService.SourceLlm, result.Source);
        Assert.Equal("Chat is the knowledge-base app on Home.", result.Answer);
        Assert.Contains(result.Links, link => link.Path == "/knowledge");
    }

    [Fact]
    public void MapDoesNotInventApps()
    {
        var paths = IntranetMap.Places
            .SelectMany(p => p.Paths.DefaultIfEmpty(p.Path))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(paths.IsSubsetOf([
            "/",
            "/knowledge",
            "/lead-inspection",
            "/sales",
            "/opportunities",
            "/pipeline",
            "/requests",
            "/sales/requests",
            "/knowledge/sources",
        ]));
        Assert.Contains(IntranetMap.Places, p => p.Id == "help");
        Assert.Contains(IntranetMap.Places, p => p.Id == "signin");
        Assert.Contains(IntranetMap.Places, p => p.Id == "requests");
        Assert.Contains(IntranetMap.Places, p => p.Id == "agent-sources");
    }

    [Fact]
    public void SuggestedQuestionsCoverTheRicherMap()
    {
        Assert.Equal(IntranetMap.FrontendCatalog.SuggestedQuestions, IntranetMap.SuggestedQuestions);
        Assert.Equal(4, IntranetMap.SuggestedQuestions.Count);
        Assert.Contains(IntranetMap.SuggestedQuestions, q => q.Contains("Chat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(IntranetMap.SuggestedQuestions, q => q.Contains("feature", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(IntranetMap.SuggestedQuestions, q => q.Contains("Bids", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(IntranetMap.SuggestedQuestions, q => q.Contains("Pipeline", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(IntranetMap.SuggestedQuestions, q => q.Contains("sign in", StringComparison.OrdinalIgnoreCase));
    }

    private static HelpAskService CreateService(IHelpLlm llm) =>
        new(llm, NullLogger<HelpAskService>.Instance);

    private sealed class NullLlm : IHelpLlm
    {
        public Task<HelpLlmTurn?> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            Task.FromResult<HelpLlmTurn?>(null);
    }

    private sealed class ScriptedLlm(string? reply) : IHelpLlm
    {
        public string LastUserPrompt { get; private set; } = string.Empty;
        public int CallCount { get; private set; }

        public Task<HelpLlmTurn?> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            CallCount++;
            LastUserPrompt = userPrompt;
            if (reply is null)
            {
                return Task.FromResult<HelpLlmTurn?>(null);
            }

            return Task.FromResult<HelpLlmTurn?>(new HelpLlmTurn(reply, "openai", "gpt-4o-mini", true));
        }
    }
}
