using System.Text.Json;
using System.Text.RegularExpressions;

namespace Intranet.Api.Help;

public sealed class HelpAskService(IHelpLlm llm, ILogger<HelpAskService> logger)
{
    public const string SourceMap = "map";
    public const string SourceLlm = "llm";

    internal const string SystemPrompt =
        """
        You are a short guide for the ETC intranet. Answer only from the intranet map.
        Never invent apps, routes, or vendors. Keep answers to 2-4 sentences.
        Prefer telling the person which Home card or Sales card to open.
        If the question is not about this intranet, say you only help with finding intranet apps.
        Reply with JSON only — no markdown fences:
        { "answer": "...", "placeIds": ["chat"] }
        placeIds must be ids from the map. Use [] if none apply.
        """;

    public async Task<HelpAskResponse> AskAsync(string? question, CancellationToken cancellationToken)
    {
        var trimmed = question?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Ask a short question about where to go in the intranet.");
        }

        if (trimmed.Length > IntranetMap.QuestionMaxLength)
        {
            throw new ArgumentException($"Keep the question under {IntranetMap.QuestionMaxLength} characters.");
        }

        var mapped = IntranetMap.Match(trimmed);
        var llmContent = await TryLlmAsync(trimmed, cancellationToken);
        if (llmContent is not null
            && TryParseLlm(llmContent, mapped, out var polished))
        {
            return polished;
        }

        return new HelpAskResponse(mapped.Answer, mapped.Links, SourceMap);
    }

    private async Task<string?> TryLlmAsync(string question, CancellationToken cancellationToken)
    {
        try
        {
            var userPrompt =
                $"""
                {IntranetMap.PromptText}

                Staff question:
                {question}
                """;
            return await llm.ChatAsync(SystemPrompt, userPrompt, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(ex, "Help LLM failed; using curated map answer.");
            return null;
        }
    }

    internal static bool TryParseLlm(
        string content,
        HelpMapAnswer mapped,
        out HelpAskResponse response)
    {
        response = new HelpAskResponse(mapped.Answer, mapped.Links, SourceMap);
        var json = UnwrapJson(content);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var answer = ReadString(doc.RootElement, "answer");
            if (string.IsNullOrWhiteSpace(answer))
            {
                return false;
            }

            var placeIds = ReadPlaceIds(doc.RootElement);
            var links = placeIds.Count > 0
                ? LinksFromIds(placeIds)
                : mapped.Links;
            if (links.Count == 0)
            {
                links = mapped.Links;
            }

            response = new HelpAskResponse(answer.Trim(), links, SourceLlm);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<HelpLinkDto> LinksFromIds(IReadOnlyList<string> placeIds)
    {
        var links = new List<HelpLinkDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in placeIds)
        {
            var place = IntranetMap.PlaceById(id);
            if (place is null || !seen.Add(place.Path))
            {
                continue;
            }

            links.Add(new HelpLinkDto(place.Title, place.Path));
        }

        return links;
    }

    private static List<string> ReadPlaceIds(JsonElement root)
    {
        if (!root.TryGetProperty("placeIds", out var value)
            && !root.TryGetProperty("place_ids", out value))
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var ids = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String
                && item.GetString() is { Length: > 0 } id)
            {
                ids.Add(id.Trim());
            }
        }

        return ids;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static string UnwrapJson(string content)
    {
        var trimmed = content.Trim();
        var fenced = Regex.Match(
            trimmed,
            @"```(?:json)?\s*([\s\S]*?)```",
            RegexOptions.IgnoreCase);
        if (fenced.Success)
        {
            return fenced.Groups[1].Value.Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : trimmed;
    }
}
