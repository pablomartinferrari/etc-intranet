using Microsoft.Extensions.Configuration;

namespace Intranet.Api.KnowledgeBase.Options;

/// <summary>
/// Re-binds nested <see cref="KnowledgeBaseOptions"/> sections from
/// <see cref="IConfiguration"/> so Azure App Settings / env vars
/// (<c>KnowledgeBase__Fallback__*</c>, <c>KnowledgeBase__Embeddings__*</c>)
/// win over empty JSON placeholders. Never log secret values.
/// </summary>
internal static class KnowledgeBaseOptionsBinder
{
    public static void BindNestedSections(KnowledgeBaseOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        options.Fallback ??= new KnowledgeBaseFallbackOptions();
        options.Embeddings ??= new KnowledgeBaseEmbeddingsOptions();

        // Bind the nested section onto the existing instance so later providers
        // (env / App Settings) overwrite appsettings.json "ApiKey": "".
        configuration.GetSection(KnowledgeBaseFallbackOptions.SectionName).Bind(options.Fallback);
        OverlayFallback(options.Fallback, configuration);

        configuration.GetSection(KnowledgeBaseEmbeddingsOptions.SectionName).Bind(options.Embeddings);
        OverlayEmbeddings(options.Embeddings, configuration);
    }

    private static void OverlayFallback(KnowledgeBaseFallbackOptions fallback, IConfiguration configuration)
    {
        if (FirstNonEmpty(configuration, "KnowledgeBase:Fallback:ApiKey", "KnowledgeBase__Fallback__ApiKey") is { } apiKey)
        {
            fallback.ApiKey = apiKey;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Fallback:BaseUrl", "KnowledgeBase__Fallback__BaseUrl") is { } baseUrl)
        {
            fallback.BaseUrl = baseUrl;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Fallback:Model", "KnowledgeBase__Fallback__Model") is { } model)
        {
            fallback.Model = model;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Fallback:ApiVersion", "KnowledgeBase__Fallback__ApiVersion") is { } apiVersion)
        {
            fallback.ApiVersion = apiVersion;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Fallback:Enabled", "KnowledgeBase__Fallback__Enabled") is { } enabled
            && bool.TryParse(enabled, out var enabledValue))
        {
            fallback.Enabled = enabledValue;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Fallback:TimeoutSeconds", "KnowledgeBase__Fallback__TimeoutSeconds") is { } timeout
            && int.TryParse(timeout, out var timeoutValue))
        {
            fallback.TimeoutSeconds = timeoutValue;
        }
    }

    private static void OverlayEmbeddings(KnowledgeBaseEmbeddingsOptions embeddings, IConfiguration configuration)
    {
        if (FirstNonEmpty(configuration, "KnowledgeBase:Embeddings:ApiKey", "KnowledgeBase__Embeddings__ApiKey") is { } apiKey)
        {
            embeddings.ApiKey = apiKey;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Embeddings:BaseUrl", "KnowledgeBase__Embeddings__BaseUrl") is { } baseUrl)
        {
            embeddings.BaseUrl = baseUrl;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Embeddings:Model", "KnowledgeBase__Embeddings__Model") is { } model)
        {
            embeddings.Model = model;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Embeddings:ApiVersion", "KnowledgeBase__Embeddings__ApiVersion") is { } apiVersion)
        {
            embeddings.ApiVersion = apiVersion;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Embeddings:Enabled", "KnowledgeBase__Embeddings__Enabled") is { } enabled
            && bool.TryParse(enabled, out var enabledValue))
        {
            embeddings.Enabled = enabledValue;
        }

        if (FirstNonEmpty(configuration, "KnowledgeBase:Embeddings:TimeoutSeconds", "KnowledgeBase__Embeddings__TimeoutSeconds") is { } timeout
            && int.TryParse(timeout, out var timeoutValue))
        {
            embeddings.TimeoutSeconds = timeoutValue;
        }
    }

    private static string? FirstNonEmpty(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
