using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.Services;

public enum ResolvedSearchMode
{
    Documents,
    Web,
    Both,
}

public sealed class ChatSearchRouter
{
    private readonly WebSearchOptions _webOptions;
    private readonly WebSearchService _webSearch;

    public ChatSearchRouter(IOptions<WebSearchOptions> webOptions, WebSearchService webSearch)
    {
        _webOptions = webOptions.Value;
        _webSearch = webSearch;
    }

    public ResolvedSearchMode Resolve(
        string? searchMode,
        bool hasDocChunks,
        double bestDocScore)
    {
        var mode = NormalizeMode(searchMode);

        return mode switch
        {
            "web" => ResolvedSearchMode.Web,
            "both" => ResolvedSearchMode.Both,
            "documents" => ResolvedSearchMode.Documents,
            _ => ResolveAuto(hasDocChunks, bestDocScore),
        };
    }

    public bool CanUseWeb(string? searchMode)
    {
        if (!_webSearch.IsAvailable)
        {
            return false;
        }

        var mode = NormalizeMode(searchMode);
        return mode is "web" or "both" or "auto";
    }

    private ResolvedSearchMode ResolveAuto(bool hasDocChunks, double bestDocScore)
    {
        if (!_webSearch.IsAvailable)
        {
            return ResolvedSearchMode.Documents;
        }

        if (!hasDocChunks)
        {
            return ResolvedSearchMode.Web;
        }

        if (bestDocScore < _webOptions.DocRelevanceThreshold)
        {
            return ResolvedSearchMode.Both;
        }

        return ResolvedSearchMode.Documents;
    }

    private static string NormalizeMode(string? searchMode) =>
        string.IsNullOrWhiteSpace(searchMode) ? "auto" : searchMode.Trim().ToLowerInvariant();
}
