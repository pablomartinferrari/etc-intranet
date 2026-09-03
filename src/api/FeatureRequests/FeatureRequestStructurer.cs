using System.Text.Json;
using System.Text.RegularExpressions;

namespace Intranet.Api.FeatureRequests;

public static class FeatureRequestStructurer
{
    public const int TitleMaxLength = 80;

    public static readonly IReadOnlyDictionary<string, string> PageContexts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["sales"] =
            "Sales hub at /sales. Staff pick Bids (/opportunities, CLEATUS recommendations) or Pipeline (/pipeline, pursuits plus close-out). " +
            "This page has no CLEATUS list. Feature notes persist via POST /api/feature-requests into IntranetDb.FeatureRequests.",
        ["opportunities"] =
            "Bids at /opportunities. Live CLEATUS list from GET /api/cleat/recommendations?minScore=80 (default). " +
            "Detail from GET /api/cleat/opportunities/{id}. Rows are not stored in IntranetDb. " +
            "Missing Cleat__ApiKey returns HTTP 503 with a setup message. Open in CLEATUS is an external link.",
        ["pipeline"] =
            "Pipeline at /pipeline. Pursuits from GET /api/cleat/pipeline (triage / preparing / submitted / won / lost / archived). " +
            "Needs close-out means overdue: past deadline, or no deadline on file. " +
            "Close-out POST /api/cleat/pursuits/{id}/close-out writes the reason to IntranetDb.PursuitCloseouts (Postgres); " +
            "CLEATUS only receives a board column_id (Won/Lost) or archived change.",
    };

    public static string SystemPrompt { get; } =
        """
        You turn a staff note into one intranet feature ticket.
        Reply with JSON only — no markdown fences, no commentary.
        Use exactly these string keys: title, problem, desiredBehavior, dataInvolved, acceptanceCriteria.
        title: short, one line.
        problem: what is missing or painful today.
        desiredBehavior: what should happen after the change.
        dataInvolved: name the real page, APIs, and tables from the page context (e.g. GET /api/cleat/recommendations, PursuitCloseouts). Do not invent vendors or secrets.
        acceptanceCriteria: newline-separated bullets an engineer can implement.
        """;

    public static string UserPrompt(string page, string rawText)
    {
        var context = PageContexts.TryGetValue(page, out var value) ? value : page;
        return
            $"""
            Page: {page}
            Page context: {context}
            Staff note:
            {rawText}
            """;
    }

    public static StructuredTicket FromFallback(string page, string rawText)
    {
        var trimmed = rawText.Trim();
        var firstLine = FirstLine(trimmed);
        var title = Truncate(firstLine, TitleMaxLength);
        var rest = trimmed.Length > firstLine.Length
            ? trimmed[firstLine.Length..].Trim()
            : string.Empty;

        var context = PageContexts.TryGetValue(page, out var value) ? value : page;
        return new StructuredTicket
        {
            Title = title,
            Problem = string.IsNullOrEmpty(rest) ? trimmed : rest,
            DesiredBehavior = string.Empty,
            DataInvolved = context,
            AcceptanceCriteria = string.Empty,
            StructuredBy = "fallback",
        };
    }

    public static StructuredTicket? TryParseLlmJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var json = UnwrapJson(content);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var title = ReadString(root, "title");
            var problem = ReadString(root, "problem");
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(problem))
            {
                return null;
            }

            return new StructuredTicket
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Feature request" : Truncate(title.Trim(), 200),
                Problem = problem?.Trim() ?? string.Empty,
                DesiredBehavior = ReadString(root, "desiredBehavior")?.Trim() ?? string.Empty,
                DataInvolved = ReadString(root, "dataInvolved")?.Trim() ?? string.Empty,
                AcceptanceCriteria = ReadString(root, "acceptanceCriteria")?.Trim() ?? string.Empty,
                StructuredBy = "llm",
            };
        }
        catch (JsonException)
        {
            return null;
        }
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
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        return trimmed;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Array => string.Join(
                "\n",
                value.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                    .Where(text => !string.IsNullOrWhiteSpace(text))),
            JsonValueKind.Null => null,
            _ => value.ToString(),
        };
    }

    private static string FirstLine(string text)
    {
        var breakAt = text.IndexOfAny(['\r', '\n']);
        return breakAt < 0 ? text : text[..breakAt].Trim();
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text[..max].TrimEnd();
    }
}
