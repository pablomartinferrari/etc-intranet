using System.Text;
using System.Text.Json;
using Intranet.Api.Help;
using Intranet.Api.KnowledgeBase;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class KnowledgeBaseOptionsBindingTests
{
    private const string JsonWithEmptyKeys =
        """
        {
          "KnowledgeBase": {
            "Fallback": {
              "Enabled": true,
              "BaseUrl": "https://api.openai.com/v1",
              "Model": "gpt-4o-mini",
              "ApiKey": "",
              "TimeoutSeconds": 30,
              "ApiVersion": "2024-10-21"
            },
            "Embeddings": {
              "Enabled": true,
              "BaseUrl": "",
              "Model": "text-embedding-3-small",
              "ApiKey": "",
              "TimeoutSeconds": 60,
              "ApiVersion": "2024-10-21"
            }
          }
        }
        """;

    [Fact]
    public void EnvFallbackApiKeyWinsOverEmptyJsonAfterConfigureAndPostConfigure()
    {
        const string envName = "KnowledgeBase__Fallback__ApiKey";
        const string envValue = "sk-unit-test-fallback-key";
        var previous = Environment.GetEnvironmentVariable(envName);
        Environment.SetEnvironmentVariable(envName, envValue);
        try
        {
            var configuration = BuildJsonThenEnvironment(JsonWithEmptyKeys);
            var options = BindWithProductionPath(configuration);

            Assert.Equal(envValue, options.Fallback.ApiKey);
            Assert.True(options.Fallback.IsConfigured);
            Assert.True(options.IsEmbeddingsConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, previous);
        }
    }

    [Fact]
    public void EnvEmbeddingsApiKeyWinsOverEmptyJsonAfterConfigureAndPostConfigure()
    {
        const string envName = "KnowledgeBase__Embeddings__ApiKey";
        const string envValue = "sk-unit-test-embeddings-key";
        var previous = Environment.GetEnvironmentVariable(envName);
        Environment.SetEnvironmentVariable(envName, envValue);
        try
        {
            var configuration = BuildJsonThenEnvironment(JsonWithEmptyKeys);
            var options = BindWithProductionPath(configuration);

            Assert.Equal(envValue, options.Embeddings.ApiKey);
            Assert.True(options.IsEmbeddingsConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, previous);
        }
    }

    [Fact]
    public void EmptyJsonWithoutEnvLeavesFallbackUnconfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(JsonWithEmptyKeys)))
            .Build();
        var options = BindWithProductionPath(configuration);

        Assert.True(string.IsNullOrWhiteSpace(options.Fallback.ApiKey));
        Assert.False(options.Fallback.IsConfigured);
        Assert.False(options.IsEmbeddingsConfigured);
    }

    [Fact]
    public void LiteralUnderscoreAppSettingKeyOverlaysEmptyJson()
    {
        // Azure Linux can surface App Settings as the literal name
        // KnowledgeBase__Fallback__ApiKey without converting __ to ':'.
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(JsonWithEmptyKeys)))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KnowledgeBase__Fallback__ApiKey"] = "sk-unit-test-literal-underscore",
                ["KnowledgeBase__Fallback__Enabled"] = "true",
                ["KnowledgeBase__Embeddings__ApiKey"] = "sk-unit-test-literal-embed",
            })
            .Build();

        var options = BindWithProductionPath(configuration);

        Assert.Equal("sk-unit-test-literal-underscore", options.Fallback.ApiKey);
        Assert.True(options.Fallback.IsConfigured);
        Assert.Equal("sk-unit-test-literal-embed", options.Embeddings.ApiKey);
        Assert.True(options.IsEmbeddingsConfigured);
    }

    [Fact]
    public void HelpStatusReturnsOnlyBooleansAndNeverSecrets()
    {
        var kb = new KnowledgeBaseOptions
        {
            Fallback = new KnowledgeBaseFallbackOptions { ApiKey = "sk-secret-must-not-appear" },
            Embeddings = new KnowledgeBaseEmbeddingsOptions { ApiKey = "sk-embed-secret-must-not-appear" },
        };
        var controller = new HelpController(
            new HelpAskService(new SilentHelpLlm(), NullLogger<HelpAskService>.Instance));

        var result = controller.Status(Options.Create(kb));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<HelpStatusResponse>(ok.Value);
        Assert.True(payload.FallbackConfigured);
        Assert.True(payload.EmbeddingsConfigured);

        var json = JsonSerializer.Serialize(payload);
        Assert.DoesNotContain("sk-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-embed", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fallbackConfigured", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("embeddingsConfigured", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HelpStatusReportsUnconfiguredWhenKeysMissing()
    {
        var controller = new HelpController(
            new HelpAskService(new SilentHelpLlm(), NullLogger<HelpAskService>.Instance));

        var result = controller.Status(Options.Create(new KnowledgeBaseOptions()));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<HelpStatusResponse>(ok.Value);
        Assert.False(payload.FallbackConfigured);
        Assert.False(payload.EmbeddingsConfigured);
    }

    private static KnowledgeBaseOptions BindWithProductionPath(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.ConfigureKnowledgeBaseOptions(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<KnowledgeBaseOptions>>().Value;
    }

    private static IConfiguration BuildJsonThenEnvironment(string json) =>
        new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .AddEnvironmentVariables()
            .Build();

    private sealed class SilentHelpLlm : IHelpLlm
    {
        public bool IsHostedFallbackConfigured => false;

        public Task<HelpLlmTurn?> ChatAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken) =>
            Task.FromResult<HelpLlmTurn?>(null);
    }
}
