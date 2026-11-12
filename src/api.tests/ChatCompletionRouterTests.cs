using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intranet.Api.Tests;

public class ChatCompletionRouterTests
{
    [Fact]
    public async Task PrefersOllamaWhenHealthy()
    {
        var ollama = new ScriptedChatClient("ollama", "llama3.1:8b", "local answer");
        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "hosted answer");
        var router = CreateRouter(ollama, hosted, ollamaHealthy: true, fallbackKey: "unit-test-placeholder-key");

        var result = await router.CompleteAsync("sys", "user", CancellationToken.None);

        Assert.Equal("local answer", result.Content);
        Assert.Equal("ollama", result.Provider);
        Assert.Equal("llama3.1:8b", result.Model);
        Assert.False(result.IsFallback);
        Assert.Equal(1, ollama.CallCount);
        Assert.Equal(0, hosted.CallCount);
    }

    [Fact]
    public async Task UsesFallbackWhenOllamaIsUnreachable()
    {
        var ollama = new ScriptedChatClient("ollama", "llama3.1:8b", "should not run");
        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "hosted answer");
        var router = CreateRouter(ollama, hosted, ollamaHealthy: false, fallbackKey: "unit-test-placeholder-key");

        var result = await router.CompleteAsync("sys", "user", CancellationToken.None);

        Assert.Equal("hosted answer", result.Content);
        Assert.Equal("openai", result.Provider);
        Assert.Equal("gpt-4o-mini", result.Model);
        Assert.True(result.IsFallback);
        Assert.Equal(0, ollama.CallCount);
        Assert.Equal(1, hosted.CallCount);
    }

    [Fact]
    public async Task NoKeyConfiguredThrowsUserReadableMessage()
    {
        var ollama = new ScriptedChatClient("ollama", "llama3.1:8b", "unused");
        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "unused");
        var router = CreateRouter(ollama, hosted, ollamaHealthy: false, fallbackKey: null);

        var ex = await Assert.ThrowsAsync<ChatUnavailableException>(
            () => router.CompleteAsync("sys", "user", CancellationToken.None));

        Assert.Contains("KnowledgeBase__Fallback__ApiKey", ex.Message, StringComparison.Ordinal);
        Assert.Contains("temporarily unavailable", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, ollama.CallCount);
        Assert.Equal(0, hosted.CallCount);
        Assert.False(router.IsFallbackConfigured);
    }

    [Fact]
    public async Task FallsBackWhenHealthyProbeThenOllamaChatFails()
    {
        var ollama = new ScriptedChatClient("ollama", "llama3.1:8b", fail: true);
        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "hosted after fail");
        var probe = new ScriptedHealthProbe(true);
        var router = CreateRouter(ollama, hosted, probe, fallbackKey: "unit-test-placeholder-key");

        var result = await router.CompleteAsync("sys", "user", CancellationToken.None);

        Assert.Equal("hosted after fail", result.Content);
        Assert.True(result.IsFallback);
        Assert.Equal(1, ollama.CallCount);
        Assert.Equal(1, hosted.CallCount);
        Assert.True(probe.Invalidated);
    }

    [Fact]
    public void FallbackIsConfiguredOnlyWhenEnabledAndKeyPresent()
    {
        Assert.False(new KnowledgeBaseFallbackOptions().IsConfigured);
        Assert.False(new KnowledgeBaseFallbackOptions { ApiKey = "  " }.IsConfigured);
        Assert.False(new KnowledgeBaseFallbackOptions { Enabled = false, ApiKey = "k" }.IsConfigured);
        Assert.True(new KnowledgeBaseFallbackOptions { ApiKey = "k" }.IsConfigured);
    }

    private static ChatCompletionRouter CreateRouter(
        IChatCompletionClient ollama,
        IChatCompletionClient hosted,
        bool ollamaHealthy,
        string? fallbackKey) =>
        CreateRouter(ollama, hosted, new ScriptedHealthProbe(ollamaHealthy), fallbackKey);

    private static ChatCompletionRouter CreateRouter(
        IChatCompletionClient ollama,
        IChatCompletionClient hosted,
        IOllamaHealthProbe probe,
        string? fallbackKey)
    {
        var fallback = new KnowledgeBaseFallbackOptions { ApiKey = fallbackKey };
        return new ChatCompletionRouter(
            ollama,
            hosted,
            probe,
            fallback,
            NullLogger<ChatCompletionRouter>.Instance);
    }

    private sealed class ScriptedChatClient : IChatCompletionClient
    {
        private readonly string _content;
        private readonly bool _fail;

        public ScriptedChatClient(string provider, string model, string content = "", bool fail = false)
        {
            ProviderName = provider;
            ModelName = model;
            _content = content;
            _fail = fail;
        }

        public string ProviderName { get; }
        public string ModelName { get; }
        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_fail)
            {
                throw new InvalidOperationException("scripted ollama failure");
            }

            return Task.FromResult(_content);
        }
    }

    private sealed class ScriptedHealthProbe : IOllamaHealthProbe
    {
        private readonly bool _healthy;

        public ScriptedHealthProbe(bool healthy) => _healthy = healthy;

        public bool Invalidated { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_healthy);

        public void Invalidate() => Invalidated = true;
    }
}
