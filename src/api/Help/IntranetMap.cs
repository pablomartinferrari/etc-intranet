using System.Text;
using System.Text.RegularExpressions;

namespace Intranet.Api.Help;

/// <summary>
/// Curated catalog of intranet apps and FAQ answers. Do not list apps that
/// are not in the SPA. Keep routes aligned with react-router paths in App.tsx.
/// </summary>
public static class IntranetMap
{
    public const int QuestionMaxLength = 400;

    public static readonly IReadOnlyList<IntranetPlace> Places =
    [
        new(
            "home",
            "Home",
            "/",
            "Intranet landing page with Chat, Lead, Sales, and Feature Requests application cards.",
            "Open the ETC logo or Applications to return to Home.",
            ["home", "intranet", "applications", "apps", "start", "landing", "welcome"]),
        new(
            "chat",
            "Chat",
            "/knowledge",
            "Company ChatGPT / knowledge-base RAG. Create a project, then start a chat, upload files, or save prompts.",
            "From Home, open the Chat card. Click New project if you do not have one yet, then New chat.",
            ["chat", "chatgpt", "knowledge", "kb", "rag", "conversation", "project", "documents", "prompts"]),
        new(
            "lead",
            "Lead",
            "/lead-inspection",
            "Multifamily LBP / lead inspection: look up a job, import XRF readings, review the grid, normalize, and generate reports.",
            "From Home, open the Lead card. Enter a job number to open the workspace.",
            ["lead", "lbp", "xrf", "inspection", "job", "jobs", "multifamily", "readings", "grid", "report", "normalize"]),
        new(
            "sales",
            "Sales",
            "/sales",
            "Sales hub. Choose Bids (CLEATUS opportunities) or Pipeline (pursuits and close-outs).",
            "From Home, open the Sales card.",
            ["sales", "hub"]),
        new(
            "bids",
            "Bids",
            "/opportunities",
            "Recommended government bids from CLEATUS. This is the opportunity list, not the pursuit board.",
            "From Home open Sales, then Bids — or go directly to Bids.",
            ["bids", "bid", "opportunities", "opportunity", "cleatus", "government", "recommendations"]),
        new(
            "pipeline",
            "Pipeline",
            "/pipeline",
            "Pursuits ETC is working, with phases (triage / preparing / submitted / won / lost / archived) and close-out.",
            "From Home open Sales, then Pipeline — or go directly to Pipeline.",
            ["pipeline", "pursuit", "pursuits", "closeout", "close-out", "won", "lost", "archived"]),
        new(
            "requests",
            "Feature Requests",
            "/requests",
            "Queue of intranet feature requests. Capture new notes and mark them planned or done.",
            "From Home, open the Feature Requests card. Use Add feature request to suggest an improvement, including topics that are not Chat, Lead, or Sales.",
            ["requests", "request", "feature", "ticket", "inbox", "change", "feedback"]),
        new(
            "agent-sources",
            "Agent sources",
            "/knowledge/sources",
            "Manage connected SharePoint folders (job status and disconnect). Add a folder from Chat or Help with Add SharePoint folder.",
            "From Chat, click Add SharePoint folder. Help has the same button. Manage connected folders from Manage sources.",
            ["agent sources", "sources", "sharepoint", "add knowledge", "chat context", "ingest", "folder", "manage sources"]),
    ];

    public static readonly IReadOnlyList<IntranetFaq> Faqs =
    [
        new(
            "create-chat",
            ["create a chat", "new chat", "start a chat", "start a conversation", "open chat", "where do i go to create", "company chatgpt", "knowledge base"],
            "Open Chat from Home (the ChatGPT / knowledge-base card). Create a project with New project if you do not have one, then click New chat. Chat is for company documents and Q&A — not this Help guide.",
            ["chat"]),
        new(
            "where-bids",
            ["where are bids", "where is bids", "find bids", "open bids", "show bids"],
            "Bids live under Sales. From Home open Sales, then the Bids card. That page lists CLEATUS government opportunities.",
            ["bids", "sales"]),
        new(
            "request-feature",
            ["request a feature", "request a change", "feature request", "submit a request", "how do i request", "file a request"],
            "Open the Feature Requests card on Home. Use Add feature request to suggest an intranet improvement — Chat, Lead, Sales, General, or another topic. Review the queue on that same page.",
            ["requests"]),
        new(
            "pipeline-vs-bids",
            ["pipeline vs bids", "bids vs pipeline", "pipeline versus bids", "bids versus pipeline", "difference between pipeline", "difference between bids", "what's pipeline vs", "whats pipeline vs", "pipeline or bids"],
            "Bids is the CLEATUS opportunity list (work you might pursue). Pipeline is deals ETC is already pursuing, plus close-out when they are won or lost. Both start from the Sales hub.",
            ["bids", "pipeline", "sales"]),
        new(
            "what-pipeline",
            ["what is pipeline", "what's pipeline", "whats pipeline", "what are pursuits"],
            "Pipeline is the pursuit board: jobs ETC is already working, with close-out when they are won or lost. It is not the same as Bids, which lists new CLEATUS opportunities.",
            ["pipeline", "bids"]),
        new(
            "what-bids",
            ["what are bids", "what is bids", "what's bids", "whats bids", "what is opportunities"],
            "Bids shows recommended government opportunities from CLEATUS. When ETC decides to pursue one, it is tracked on Pipeline.",
            ["bids", "pipeline"]),
        new(
            "what-lead",
            ["what is lead", "what's lead", "lead inspection", "xrf", "lbp"],
            "Lead is the multifamily LBP workspace. Look up a job number, import XRF readings, review the grid, normalize results, and generate reports.",
            ["lead"]),
        new(
            "what-home",
            ["what is home", "where is home", "applications", "back to home"],
            "Home is the intranet landing page. After you sign in you will see Chat, Lead, Sales, and Feature Requests. The ETC logo always returns here.",
            ["home"]),
        new(
            "agent-sources",
            ["add knowledge", "agent sources", "sharepoint folder", "add to chat context", "connect sharepoint", "chat sources", "add sharepoint folder", "index this sharepoint folder", "index this sharepoint"],
            "In Chat, click Add SharePoint folder (also in Help). Paste a site URL and folder path, review the size estimate, and connect. Manage connected folders from Manage sources. Huge folders file an admin request instead of ingesting automatically.",
            ["chat", "agent-sources"]),
    ];

    public static readonly IReadOnlyList<string> SuggestedQuestions =
    [
        "Where do I go to create a chat?",
        "Where are bids?",
        "How do I request a feature?",
        "What's Pipeline vs Bids?",
    ];

    public static string PromptText { get; } = BuildPromptText();

    public static HelpMapAnswer Match(string question)
    {
        var normalized = Normalize(question);
        if (string.IsNullOrEmpty(normalized))
        {
            return Overview();
        }

        var faq = MatchFaq(normalized);
        if (faq is not null)
        {
            return FromPlaces(faq.Answer, faq.PlaceIds);
        }

        var comparison = IsComparison(normalized);
        var scored = ScorePlaces(normalized);
        if (comparison && scored.Count >= 2)
        {
            var top = scored.Take(2).Select(s => s.Place.Id).ToArray();
            var titles = string.Join(" and ", top.Select(IdToTitle));
            return FromPlaces(
                $"{titles} are different Sales apps. " +
                "Bids lists CLEATUS opportunities; Pipeline tracks pursuits and close-outs. " +
                "Open Sales first if you are not sure which you need.",
                top.Concat(["sales"]).Distinct(StringComparer.Ordinal).ToArray());
        }

        if (scored.Count > 0 && scored[0].Score >= 2)
        {
            var place = scored[0].Place;
            return FromPlaces(
                $"{place.Title} — {place.Purpose} {place.HowToGetThere}",
                [place.Id]);
        }

        if (scored.Count > 0 && scored[0].Score >= 1)
        {
            var place = scored[0].Place;
            return FromPlaces(
                $"{place.Title} is at {place.Path}. {place.Purpose} {place.HowToGetThere}",
                [place.Id]);
        }

        return Overview();
    }

    public static IntranetPlace? PlaceById(string id) =>
        Places.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    private static HelpMapAnswer Overview() =>
        FromPlaces(
            "This intranet has four Home apps: Chat (company knowledge-base ChatGPT), Lead (multifamily LBP / XRF jobs), Sales (Bids from CLEATUS, plus Pipeline pursuits), and Feature Requests (suggest and track intranet improvements). Ask where you want to go, or open Home to pick a card.",
            ["home", "chat", "lead", "sales"]);

    private static HelpMapAnswer FromPlaces(string answer, IReadOnlyList<string> placeIds)
    {
        var links = new List<HelpLinkDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in placeIds)
        {
            var place = PlaceById(id);
            if (place is null || !seen.Add(place.Path))
            {
                continue;
            }

            links.Add(new HelpLinkDto(place.Title, place.Path));
        }

        return new HelpMapAnswer(answer, links, placeIds);
    }

    private static IntranetFaq? MatchFaq(string normalized)
    {
        IntranetFaq? best = null;
        var bestLen = 0;
        foreach (var faq in Faqs)
        {
            foreach (var phrase in faq.Phrases)
            {
                var needle = Normalize(phrase);
                if (needle.Length == 0 || !normalized.Contains(needle, StringComparison.Ordinal))
                {
                    continue;
                }

                if (needle.Length > bestLen)
                {
                    best = faq;
                    bestLen = needle.Length;
                }
            }
        }

        return best;
    }

    private static List<(IntranetPlace Place, int Score)> ScorePlaces(string normalized)
    {
        var tokens = Tokenize(normalized);
        var scored = new List<(IntranetPlace Place, int Score)>();
        foreach (var place in Places)
        {
            var score = 0;
            foreach (var keyword in place.Keywords)
            {
                var key = Normalize(keyword);
                if (key.Length == 0)
                {
                    continue;
                }

                if (key.Contains(' ', StringComparison.Ordinal))
                {
                    if (normalized.Contains(key, StringComparison.Ordinal))
                    {
                        score += 3;
                    }

                    continue;
                }

                if (tokens.Contains(key))
                {
                    score += key.Length >= 5 ? 2 : 1;
                }
            }

            if (normalized.Contains(Normalize(place.Title), StringComparison.Ordinal))
            {
                score += 3;
            }

            if (score > 0)
            {
                scored.Add((place, score));
            }
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Place.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsComparison(string normalized) =>
        normalized.Contains(" vs ", StringComparison.Ordinal)
        || normalized.Contains(" versus ", StringComparison.Ordinal)
        || normalized.Contains("difference", StringComparison.Ordinal)
        || normalized.Contains("compared", StringComparison.Ordinal);

    private static string IdToTitle(string id) => PlaceById(id)?.Title ?? id;

    internal static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lowered = text.Trim().ToLowerInvariant();
        var builder = new StringBuilder(lowered.Length);
        foreach (var ch in lowered)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static HashSet<string> Tokenize(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    private static string BuildPromptText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ETC intranet map (only these apps exist):");
        foreach (var place in Places)
        {
            sb.AppendLine($"- id={place.Id}; title={place.Title}; path={place.Path}");
            sb.AppendLine($"  purpose: {place.Purpose}");
            sb.AppendLine($"  how: {place.HowToGetThere}");
        }

        sb.AppendLine();
        sb.AppendLine("Starter questions staff often ask:");
        foreach (var q in SuggestedQuestions)
        {
            sb.AppendLine($"- {q}");
        }

        return sb.ToString();
    }
}
