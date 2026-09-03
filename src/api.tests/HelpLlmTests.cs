using Intranet.Api.Help;
using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class HelpLlmTests
{
    [Fact]
    public async Task UsesHostedRouterWhenOllamaIsDown()
    {
        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", """{ "answer": "ok", "placeIds": [] }""");
        var llm = CreateLlm(CreateRouter(hosted, ollamaHealthy: false));

        var turn = await llm.ChatAsync("sys", "user", CancellationToken.None);

        Assert.NotNull(turn);
        Assert.Equal("openai", turn!.Provider);
        Assert.Equal("gpt-4o-mini", turn.Model);
        Assert.True(turn.IsFallback);
        Assert.Contains("ok", turn.Content, StringComparison.Ordinal);
        Assert.Equal(1, hosted.CallCount);
        Assert.True(llm.IsHostedFallbackConfigured);
    }

    [Fact]
    public async Task ReturnsNullWhenNoModelIsConfigured()
    {
        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "unused");
        var llm = CreateLlm(CreateRouter(hosted, ollamaHealthy: false, fallbackKey: null));

        var turn = await llm.ChatAsync("sys", "user", CancellationToken.None);

        Assert.Null(turn);
        Assert.Equal(0, hosted.CallCount);
        Assert.False(llm.IsHostedFallbackConfigured);
    }

    [Fact]
    public void CallTimeoutExceedsHostedHttpTimeoutSoFallbackIsNotCancelled()
    {
        var fallback = new KnowledgeBaseFallbackOptions { TimeoutSeconds = 30 };
        var timeout = HelpLlm.ResolveTimeout(fallback);

        Assert.True(timeout > TimeSpan.FromSeconds(fallback.TimeoutSeconds));
        Assert.Equal(TimeSpan.FromSeconds(40), timeout);
        Assert.True(timeout > TimeSpan.FromSeconds(25));
    }

    private static HelpLlm CreateLlm(ChatCompletionRouter router, int timeoutSeconds = 30) =>
        new(
            router,
            Options.Create(new KnowledgeBaseOptions
            {
                Fallback = new KnowledgeBaseFallbackOptions { TimeoutSeconds = timeoutSeconds },
            }),
            NullLogger<HelpLlm>.Instance);

    private static ChatCompletionRouter CreateRouter(
        IChatCompletionClient hosted,
        bool ollamaHealthy,
        string? fallbackKey = "unit-test-placeholder-key")
    {
        var ollama = new ScriptedChatClient("ollama", "llama3.1:8b", "local");
        return new ChatCompletionRouter(
            ollama,
            hosted,
            new ScriptedHealthProbe(ollamaHealthy),
            new KnowledgeBaseFallbackOptions { ApiKey = fallbackKey },
            NullLogger<ChatCompletionRouter>.Instance);
    }

    private sealed class ScriptedChatClient : IChatCompletionClient
    {
        private readonly string _content;

        public ScriptedChatClient(string provider, string model, string content)
        {
            ProviderName = provider;
            ModelName = model;
            _content = content;
        }

        public string ProviderName { get; }
        public string ModelName { get; }
        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_content);
        }
    }

    private sealed class ScriptedHealthProbe(bool healthy) : IOllamaHealthProbe
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(healthy);

        public void Invalidate()
        {
        }
    }
}
