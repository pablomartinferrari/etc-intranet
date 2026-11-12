using System.Net;
using System.Text;
using Intranet.Api.KnowledgeBase.Services;
using Xunit;

namespace Intranet.Api.Tests;

public class OpenAiCompatibleChatClientTests
{
    [Fact]
    public void BuildsOpenAiBearerRequest()
    {
        using var request = OpenAiCompatibleChatClient.BuildRequest(
            "https://api.openai.com/v1",
            "gpt-4o-mini",
            "unit-test-placeholder-key",
            "2024-10-21",
            "sys",
            "user");

        Assert.Equal("https://api.openai.com/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("unit-test-placeholder-key", request.Headers.Authorization?.Parameter);
        Assert.False(request.Headers.Contains("api-key"));
    }

    [Fact]
    public void BuildsAzureOpenAiRequestWithDeploymentAndApiKey()
    {
        using var request = OpenAiCompatibleChatClient.BuildRequest(
            "https://etc-openai.openai.azure.com",
            "intranet-mini",
            "unit-test-placeholder-key",
            "2024-10-21",
            "sys",
            "user");

        Assert.Equal(
            "https://etc-openai.openai.azure.com/openai/deployments/intranet-mini/chat/completions?api-version=2024-10-21",
            request.RequestUri!.ToString());
        Assert.Null(request.Headers.Authorization);
        Assert.Equal("unit-test-placeholder-key", request.Headers.GetValues("api-key").Single());
    }

    [Fact]
    public void UsesDeploymentPathWhenProvided()
    {
        var url = OpenAiCompatibleChatClient.ResolveCompletionsUrl(
            "https://etc-openai.openai.azure.com/openai/deployments/prod-chat",
            "ignored-model",
            "2024-06-01",
            azure: true);

        Assert.Equal(
            "https://etc-openai.openai.azure.com/openai/deployments/prod-chat/chat/completions?api-version=2024-06-01",
            url);
    }

    [Fact]
    public void DetectsAzureHosts()
    {
        Assert.True(OpenAiCompatibleChatClient.IsAzureOpenAi("https://res.openai.azure.com"));
        Assert.True(OpenAiCompatibleChatClient.IsAzureOpenAi("https://res.cognitiveservices.azure.com"));
        Assert.False(OpenAiCompatibleChatClient.IsAzureOpenAi("https://api.openai.com/v1"));
        Assert.False(OpenAiCompatibleChatClient.IsAzureOpenAi("http://localhost:11434"));
    }

    [Fact]
    public async Task HealthProbeCachesDownResult()
    {
        var handler = new CountingHandler { Throw = true };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var options = Microsoft.Extensions.Options.Options.Create(
            new Intranet.Api.KnowledgeBase.Options.KnowledgeBaseOptions
            {
                OllamaBaseUrl = "http://127.0.0.1:9",
            });
        var probe = new OllamaHealthProbe(http, options, Microsoft.Extensions.Logging.Abstractions.NullLogger<OllamaHealthProbe>.Instance);

        Assert.False(await probe.IsAvailableAsync());
        Assert.False(await probe.IsAvailableAsync());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void LegacyCitationArrayStillParses()
    {
        var json = """[{"Type":"document","Title":"Spec","Snippet":"hi"}]""";
        var (citations, generation) = RagService.ParseMessagePayload(json);
        Assert.NotNull(citations);
        Assert.Equal("Spec", Assert.Single(citations!).Title);
        Assert.Null(generation);
    }

    [Fact]
    public void SerializesGenerationWithCitations()
    {
        var json = RagService.SerializeMessagePayload(
            [new Intranet.Api.KnowledgeBase.Models.CitationDto("document", "Spec", "hi")],
            new Intranet.Api.KnowledgeBase.Models.ChatGenerationDto("openai", "gpt-4o-mini", true));

        var (citations, generation) = RagService.ParseMessagePayload(json);
        Assert.Equal("Spec", Assert.Single(citations!).Title);
        Assert.NotNull(generation);
        Assert.True(generation!.IsFallback);
        Assert.Equal("openai", generation.Provider);
        Assert.Equal("gpt-4o-mini", generation.Model);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool Throw { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (Throw)
            {
                throw new HttpRequestException("connection refused");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"models":[]}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
