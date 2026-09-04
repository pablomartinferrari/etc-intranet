using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Intranet.Api.Help;

/// <summary>
/// Curated catalog of intranet apps and FAQ answers. Do not list apps that
/// are not in the SPA. Keep routes aligned with react-router paths in App.tsx.
/// This is the source of truth for Help prompts, map fallback, and the
/// frontend starter catalog (<c>src/web/src/help/intranet-map.json</c>).
/// </summary>
public static class IntranetMap
{
    public const int QuestionMaxLength = 400;

    /// <summary>Minimum token-overlap score before a place is treated as a hit.</summary>
    public const int MinMatchScore = 2;

    public static readonly IReadOnlyList<IntranetPlace> Places =
    [
        new(
            "home",
            "Home",
            ["intranet", "landing", "applications", "apps", "start", "welcome", "home page", "etc intranet"],
            "/",
            ["/"],
            "Open the ETC logo in the header, or choose Applications after you sign in.",
            "Intranet landing page. After Microsoft Entra sign-in it shows four application cards: Chat, Lead, Sales, and Feature Requests. Unsigned visitors only see Sign in with Microsoft.",
            ["chat", "lead", "sales", "requests", "signin"],
            [
                "What is Home?",
                "Where is Home?",
                "How do I get back to applications?",
                "What apps are on the intranet?",
            ],
            ["SPA only — no separate data store"],
            "Every signed-in ETC employee.",
            "Home is the intranet landing page. After you sign in with Microsoft you will see Chat, Lead, Sales, and Feature Requests. The ETC logo in the header always returns here."),
        new(
            "chat",
            "Chat",
            [
                "chatgpt", "knowledge", "kb", "rag", "conversation", "company chatgpt", "knowledge base",
                "documents", "prompts", "project", "projects", "chats", "new chat", "multiple chats",
                "second chat", "share project", "share chat", "share", "project files",
                "sharepoint folder", "add sharepoint folder",
            ],
            "/knowledge",
            ["/knowledge"],
            "From Home, open the Chat card. Use New project if you do not have one. The ChatGPT-style sidebar lists areas and projects; expand a project to see its chats underneath. Click New chat (or + New chat under the project) to start a blank thread in the selected project — a New chat draft shows under the project, and after the first message it is listed there. Project files and Prompts open in sheets. Sources in the sidebar opens SharePoint folders at /knowledge/sources. Project owners share the project (files plus the ability to chat, not a single-thread export) with Entra users or groups from the Share control on the project row or the chat header.",
            "Company ChatGPT / knowledge-base RAG with a single ChatGPT-style sidebar. Yes: a project can have many chats. New chat starts a blank thread in the selected project; after the first message it appears under that project. Upload files and save prompts via sheets. Answers use project documents and connected SharePoint folders. A compact N-sources chip on an answer opens an optional citation side panel — that is not the Sources nav. Owners share the whole project with Entra users or groups (viewer or editor). Local Ollama when the GPU VM is up; otherwise a hosted OpenAI-compatible model when KnowledgeBase Fallback is configured. This is not the floating Help panel.",
            ["home", "help", "requests", "agent-sources"],
            [
                "Where is Chat?",
                "Where do I go to create a chat?",
                "How do I start a new chat?",
                "Where is the company ChatGPT?",
                "How do I upload documents to the knowledge base?",
                "Where are prompts?",
                "Add SharePoint folder",
                "Index this SharePoint folder",
                "How do I add a SharePoint folder?",
                "Can I have multiple chats in one project?",
                "How do I start a second chat in a project?",
                "How do I share a Chat project?",
                "Can I add multiple chats to a single project?",
            ],
            ["Postgres knowledge DB (pgvector)", "uploaded project files", "SharePoint folders via Microsoft Graph", "Ollama and/or hosted OpenAI-compatible chat"],
            "Every ETC employee. Separate from Help — Help only knows the intranet map.",
            "Yes — one Chat project can have many chats. Open Chat from Home, select the project, and click New chat (or + New chat under that project). A New chat draft appears under the project; after you send the first message it becomes a listed thread. Files and Prompts are sheets, not tabs. Sources in the sidebar is SharePoint folders, not the answer citation panel (that is the compact \"N sources\" control). Owners share the project — files and the ability to chat — with Entra users or groups from Share; it is not a single-thread export.",
            [
                "Yes, you can add multiple chats to a single project. New chat starts a blank thread in the selected project.",
                "Share is project-level (files plus the ability to chat), not a single-thread export. Owners open Share from the project row or the chat header.",
                "Project files and Prompts open in sheets. Sources in the Chat sidebar is SharePoint folders — not the citation side panel.",
            ]),
        new(
            "lead",
            "Lead",
            ["lbp", "xrf", "inspection", "job", "jobs", "multifamily", "readings", "grid", "report", "normalize", "lead inspection", "job number"],
            "/lead-inspection",
            ["/lead-inspection"],
            "From Home, open the Lead card. Enter a job number to look up or open the multifamily LBP workspace. SharePoint deep links under /jobs/... return here after sign-in.",
            "Multifamily lead-based paint (LBP) inspection workspace. Look up a job, import XRF workbooks from SharePoint (XRF-SourceFiles library / Lead Inspection Upload web part), review the grid, normalize readings, and generate reports.",
            ["home", "signin", "requests"],
            [
                "What is Lead?",
                "Where is lead inspection?",
                "How do I open a job?",
                "Where do I import XRF readings?",
                "How does SharePoint import work for Lead?",
            ],
            ["Postgres (jobs and readings)", "SharePoint XRF-SourceFiles via Microsoft Graph"],
            "Inspection staff working multifamily LBP jobs.",
            "Lead is the multifamily LBP workspace. From Home open Lead, enter a job number, then import XRF files from SharePoint, review the grid, normalize, and generate reports."),
        new(
            "sales",
            "Sales",
            ["sales hub", "sales home", "sales card"],
            "/sales",
            ["/sales"],
            "From Home, open the Sales card. Then pick Bids or Pipeline. The Requests button on this page goes to Feature Requests.",
            "Sales hub. Choose Bids (CLEATUS government opportunities) or Pipeline (pursuits and close-outs). This page does not list bids itself.",
            ["bids", "pipeline", "requests", "home"],
            [
                "Where is Sales?",
                "What is the Sales hub?",
                "How do I get to Bids or Pipeline?",
            ],
            ["None on this page — children use CLEATUS and Postgres"],
            "Business development and anyone tracking pursuits.",
            "Sales is the hub for Bids and Pipeline. From Home open the Sales card, then choose Bids (CLEATUS opportunities) or Pipeline (pursuits and close-out). Feature Requests is a separate Home card, with a shortcut from Sales."),
        new(
            "bids",
            "Bids",
            ["bid", "opportunities", "opportunity", "cleatus", "government", "recommendations", "sam", "set-aside", "naics"],
            "/opportunities",
            ["/opportunities"],
            "From Home open Sales, then Bids — or go directly to Bids.",
            "Recommended government bids from CLEATUS. This is the opportunity list, not the pursuit board. Filter by search, deadline, NAICS, and set-aside. Default minimum recommendation score is 80. When ETC decides to pursue a bid, it is tracked on Pipeline.",
            ["pipeline", "sales"],
            [
                "Where are bids?",
                "Where is Bids?",
                "What are bids?",
                "What is the opportunities list?",
                "How do I find government opportunities?",
            ],
            ["CLEATUS"],
            "Business development reviewing new government work.",
            "Bids live under Sales. From Home open Sales, then the Bids card. That page lists recommended CLEATUS government opportunities. It is not Pipeline — Pipeline tracks deals ETC is already pursuing."),
        new(
            "pipeline",
            "Pipeline",
            ["pursuit", "pursuits", "closeout", "close-out", "won", "lost", "archived", "triage", "preparing", "submitted", "needs close-out"],
            "/pipeline",
            ["/pipeline"],
            "From Home open Sales, then Pipeline — or go directly to Pipeline.",
            "Pursuits ETC is already working. Phases are triage, preparing, submitted, won, lost, and archived. Close-out records why a pursuit was won, lost, or dropped. Needs close-out highlights overdue items. This is not the Bids opportunity list.",
            ["bids", "sales"],
            [
                "What is Pipeline?",
                "What are pursuits?",
                "Where is close-out?",
                "What do triage, preparing, and submitted mean?",
                "What's Pipeline vs Bids?",
            ],
            ["CLEATUS", "intranet Postgres (close-out reasons)"],
            "Business development tracking active pursuits.",
            "Pipeline is the pursuit board: jobs ETC is already working, with phases triage / preparing / submitted / won / lost / archived, plus close-out. It is not Bids, which lists new CLEATUS opportunities. Open Sales first if you are not sure which you need."),
        new(
            "requests",
            "Feature Requests",
            ["request", "requests", "feature", "ticket", "inbox", "change", "feedback", "feature request", "suggest", "planned", "done", "queue"],
            "/requests",
            ["/requests", "/sales/requests"],
            "From Home, open the Feature Requests card. Use Add feature request, pick an area (Chat, Lead, Sales, General, or Other with a topic name), and write a short note. Sales also has a Requests shortcut. /sales/requests is the same queue.",
            "Queue of intranet improvement ideas stored in Postgres. New notes can be structured by an optional local model; they still save if that model is down. Reviewers mark a row new → planned → done. Planned means it was picked up. Done means the work finished. New is the default just after capture. Areas include Chat, Lead, Sales, General, and Other.",
            ["home", "sales", "help"],
            [
                "How do I request a feature?",
                "How do I submit a feature request?",
                "Where is the requests queue?",
                "What do new, planned, and done mean?",
                "How do I mark a request planned or done?",
            ],
            ["intranet Postgres", "optional local LLM to structure the note", "Twilio SMS notify (when configured)"],
            "Any signed-in staff can capture a request. Reviewers update status.",
            "Open the Feature Requests card on Home (or Requests from Sales). Use Add feature request, pick Chat / Lead / Sales / General / Other, and write a note. The queue statuses are new (just captured), planned (picked up), and done (finished). Change status on a row in that same page."),
        new(
            "agent-sources",
            "Agent sources",
            ["agent sources", "sources", "sharepoint", "add knowledge", "chat context", "ingest", "manage sources", "connect sharepoint", "chat sources"],
            "/knowledge/sources",
            ["/knowledge/sources"],
            "From Chat, click Sources in the sidebar (or open /knowledge/sources). Help also has Add SharePoint folder. This page lists connected SharePoint folders — it is not the compact \"N sources\" citation panel on a Chat answer.",
            "Connected SharePoint folders used as intranet-wide Chat context. Add a folder from Chat or Help, review the size estimate, and connect. Huge folders file a Feature Request instead of ingesting automatically. This page shows job status and disconnect. It is not the optional citation side panel.",
            ["chat", "help", "requests"],
            [
                "How do I add knowledge?",
                "How do I connect SharePoint?",
                "Where are agent sources?",
                "Where is Manage sources?",
                "Where is Sources?",
                "How do I disconnect a SharePoint folder?",
            ],
            ["Microsoft Graph (Sites.Read.All, Files.Read.All)", "Postgres knowledge documents", "hosted embeddings"],
            "Every signed-in ETC employee. Add is self-serve within size limits.",
            "In Chat, open Sources in the sidebar (Help also has Add SharePoint folder). Paste a site URL and folder path, review the size estimate, and connect. Manage connected folders on this page. Huge folders file an admin request instead of ingesting automatically. The compact \"N sources\" chip on a Chat answer is a different citation panel."),
        new(
            "help",
            "Help",
            ["help agent", "side panel", "guide", "this panel", "help button", "intranet help"],
            "",
            [],
            "After you sign in, click the floating Help button at the bottom-right of any intranet page. This opens this side panel. There is no separate Help route.",
            "Short in-app guide for finding intranet apps. It answers from an AI model grounded only on this curated map (Ollama when healthy, else the same hosted KnowledgeBase Fallback chat as company Chat), or from map scoring when no model is available. It does not search Chat documents and is not company ChatGPT.",
            ["chat", "home", "requests", "agent-sources"],
            [
                "What is this Help panel?",
                "How does Help work?",
                "Is Help the same as Chat?",
                "Where is the Help button?",
            ],
            ["in-repo intranet map", "Ollama and/or KnowledgeBase Fallback chat (same router as Chat)"],
            "Signed-in staff only — the Help button is hidden until Entra sign-in.",
            "Help is this side panel (the floating Help button), not Chat. It only knows the intranet map: Home, Chat, Lead, Sales, Bids, Pipeline, Feature Requests, Agent sources, and sign-in. Chat is the knowledge-base ChatGPT under the Chat card on Home. Use Add SharePoint folder at the top of this panel to connect documents for Chat."),
        new(
            "signin",
            "Sign in",
            ["sign in", "signin", "login", "log in", "microsoft", "entra", "azure ad", "sso", "authentication", "work account", "sign-in"],
            "/",
            ["/"],
            "On Home, click Sign in with Microsoft. Microsoft Entra ID (work account) redirects back to this intranet. A saved deep link (for example a Lead job) is restored after login. Sign out is in the header.",
            "Gates every intranet app. Use your Microsoft work account (Entra ID / Azure AD). The Help button appears only after sign-in. API calls use the same Entra access token.",
            ["home", "lead"],
            [
                "How do I sign in?",
                "How do I log in?",
                "How does Microsoft Entra sign-in work?",
                "Where is Sign in with Microsoft?",
            ],
            ["Microsoft Entra ID (MSAL in the browser, JWT Bearer on the API)"],
            "All ETC staff with a work account.",
            "Sign in on Home with Sign in with Microsoft (Entra ID / your work account). After the redirect you land on Home or a saved deep link such as a Lead job. The Help button and application cards appear only once you are signed in."),
    ];

    public static readonly IReadOnlyList<string> SuggestedQuestions =
    [
        "Where is Chat?",
        "How do I request a feature?",
        "What's Bids vs Pipeline?",
        "How do I sign in?",
    ];

    private static readonly JsonSerializerOptions PromptJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions FrontendJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string PromptJson { get; } = BuildPromptJson();

    public static string PromptText =>
        "ETC intranet map JSON (only these places exist; never invent routes):\n" + PromptJson;

    public static HelpFrontendCatalog FrontendCatalog { get; } = BuildFrontendCatalog();

    public static string FrontendCatalogJson { get; } = BuildFrontendCatalogJson();

    public static HelpMapAnswer Match(string question)
    {
        var normalized = Normalize(question);
        if (string.IsNullOrEmpty(normalized))
        {
            return Overview();
        }

        var scored = ScorePlaces(normalized);
        if (scored.Count == 0 || scored[0].Score < MinMatchScore)
        {
            return Overview();
        }

        if (IsComparison(normalized) && DistinctTopPlaces(scored, 2).Count >= 2)
        {
            var top = DistinctTopPlaces(scored, 2);
            var ids = top.Select(s => s.Place.Id).ToList();
            if (ids.Contains("bids") && ids.Contains("pipeline") && !ids.Contains("sales"))
            {
                ids.Add("sales");
            }

            return FromPlaces(ComparisonAnswer(top[0].Place, top[1].Place), ids);
        }

        if (ShouldListMatches(scored))
        {
            var listed = DistinctTopPlaces(scored, 3);
            var titles = string.Join(", ", listed.Select(s => s.Place.Title));
            return FromPlaces(
                $"I can help with {titles}. Ask about one of those, or open a link below.",
                listed.Select(s => s.Place.Id).ToArray());
        }

        var place = scored[0].Place;
        return FromPlaces(place.FallbackAnswer, [place.Id]);
    }

    /// <summary>Ranked place scores for tests and diagnostics. Empty when nothing overlaps.</summary>
    public static IReadOnlyList<(string PlaceId, int Score)> Rank(string question)
    {
        var normalized = Normalize(question);
        if (string.IsNullOrEmpty(normalized))
        {
            return [];
        }

        return ScorePlaces(normalized)
            .Select(s => (s.Place.Id, s.Score))
            .ToList();
    }

    public static IntranetPlace? PlaceById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var trimmed = id.Trim();
        return Places.FirstOrDefault(p => string.Equals(p.Id, trimmed, StringComparison.OrdinalIgnoreCase))
            ?? Places.FirstOrDefault(p => string.Equals(p.Title, trimmed, StringComparison.OrdinalIgnoreCase));
    }

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

    private static HelpMapAnswer Overview() =>
        FromPlaces(
            "This intranet has four Home apps: Chat (company knowledge-base ChatGPT), Lead (multifamily LBP / XRF jobs), Sales (Bids from CLEATUS, plus Pipeline pursuits), and Feature Requests (suggest and track intranet improvements). Sign in with Microsoft on Home. The floating Help button is this guide — not Chat. Ask where you want to go, or open Home to pick a card.",
            ["home", "chat", "lead", "sales", "requests"]);

    private static string ComparisonAnswer(IntranetPlace left, IntranetPlace right)
    {
        var pair = new HashSet<string>([left.Id, right.Id], StringComparer.OrdinalIgnoreCase);
        if (pair.SetEquals(["bids", "pipeline"]))
        {
            return "Bids is the CLEATUS opportunity list (work you might pursue). Pipeline is deals ETC is already pursuing, with phases and close-out when they are won or lost. Both start from the Sales hub.";
        }

        return $"{left.Title} and {right.Title} are different places. {left.Title}: {left.Purpose} {right.Title}: {right.Purpose} Open Home if you are not sure which card to use.";
    }

    private static HelpMapAnswer FromPlaces(string answer, IReadOnlyList<string> placeIds)
    {
        var links = new List<HelpLinkDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in placeIds)
        {
            var place = PlaceById(id);
            if (place is null || string.IsNullOrWhiteSpace(place.Path) || !seen.Add(place.Path))
            {
                continue;
            }

            links.Add(new HelpLinkDto(place.Title, place.Path));
        }

        return new HelpMapAnswer(answer, links, placeIds);
    }

    private static List<(IntranetPlace Place, int Score)> ScorePlaces(string normalized)
    {
        var tokens = ContentTokens(normalized);
        var scored = new List<(IntranetPlace Place, int Score)>();
        foreach (var place in Places)
        {
            var score = ScorePlace(place, normalized, tokens);
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

    private static int ScorePlace(IntranetPlace place, string normalized, HashSet<string> tokens)
    {
        var score = 0;
        var titleNorm = Normalize(place.Title);
        if (titleNorm.Length > 0 && normalized.Contains(titleNorm, StringComparison.Ordinal))
        {
            score += 8;
        }

        foreach (var part in titleNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (tokens.Contains(part))
            {
                score += part.Length >= 5 ? 4 : 3;
            }
        }

        foreach (var alias in place.Aliases)
        {
            var key = Normalize(alias);
            if (key.Length == 0)
            {
                continue;
            }

            if (key.Contains(' ', StringComparison.Ordinal))
            {
                if (normalized.Contains(key, StringComparison.Ordinal))
                {
                    score += 7;
                }

                continue;
            }

            if (tokens.Contains(key))
            {
                score += key.Length >= 5 ? 5 : 3;
            }
        }

        foreach (var common in place.CommonQuestions)
        {
            var needle = Normalize(common);
            if (needle.Length == 0)
            {
                continue;
            }

            if (normalized.Contains(needle, StringComparison.Ordinal)
                || (needle.Length > 12 && needle.Contains(normalized, StringComparison.Ordinal)))
            {
                score += 12;
                continue;
            }

            var cqTokens = ContentTokens(needle);
            if (cqTokens.Count == 0)
            {
                continue;
            }

            var overlap = cqTokens.Count(tokens.Contains);
            if (overlap >= 2 && overlap * 10 >= cqTokens.Count * 6)
            {
                score += 8;
            }
        }

        var haystack = Normalize(string.Join(
            ' ',
            new[] { place.Purpose, place.HowToGetThere, place.AudienceNotes ?? "", place.FallbackAnswer }
                .Concat(place.DataSources)
                .Concat(place.Tips ?? [])));
        var hayTokens = Tokenize(haystack);
        foreach (var token in tokens)
        {
            if (!hayTokens.Contains(token))
            {
                continue;
            }

            score += token.Length >= 5 ? 2 : 1;
        }

        return score;
    }

    private static bool ShouldListMatches(List<(IntranetPlace Place, int Score)> scored)
    {
        if (scored.Count < 2)
        {
            return false;
        }

        var top = scored[0].Score;
        var second = scored[1].Score;
        if (second < MinMatchScore)
        {
            return false;
        }

        // Distinct titles sharing a path (e.g. Home vs Sign in) still count as one Open link;
        // only list when the runner-up is close and not just a weak haystack hit.
        return second * 4 >= top * 3 && second >= 5;
    }

    private static List<(IntranetPlace Place, int Score)> DistinctTopPlaces(
        List<(IntranetPlace Place, int Score)> scored,
        int take)
    {
        var result = new List<(IntranetPlace Place, int Score)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in scored)
        {
            if (!seen.Add(row.Place.Id))
            {
                continue;
            }

            result.Add(row);
            if (result.Count >= take)
            {
                break;
            }
        }

        return result;
    }

    private static bool IsComparison(string normalized) =>
        normalized.Contains(" vs ", StringComparison.Ordinal)
        || normalized.StartsWith("vs ", StringComparison.Ordinal)
        || normalized.Contains(" versus ", StringComparison.Ordinal)
        || normalized.Contains("difference", StringComparison.Ordinal)
        || normalized.Contains("compared", StringComparison.Ordinal)
        || (normalized.Contains("bids", StringComparison.Ordinal)
            && normalized.Contains("pipeline", StringComparison.Ordinal));

    private static HashSet<string> ContentTokens(string normalized)
    {
        var tokens = Tokenize(normalized);
        tokens.ExceptWith(Stopwords);
        return tokens;
    }

    private static HashSet<string> Tokenize(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "is", "are", "am", "was", "were", "be", "been", "being",
        "do", "does", "did", "to", "of", "in", "on", "at", "for", "and", "or", "but",
        "if", "then", "than", "so", "as", "by", "from", "with", "about", "into",
        "over", "after", "before", "up", "down", "out", "off", "not", "no", "yes",
        "can", "could", "should", "would", "will", "just", "please", "me", "my",
        "we", "our", "you", "your", "it", "this", "that", "these", "those",
        "where", "what", "whats", "how", "why", "who", "which", "go", "going",
        "get", "got", "find", "show", "tell", "use", "using", "want", "need",
        "there", "here", "also", "any", "some", "only", "own", "same",
        "i", "im", "ive", "dont", "doesnt", "cant", "wheres",
    };

    private static string BuildPromptJson()
    {
        var payload = Places.Select(p => new
        {
            id = p.Id,
            title = p.Title,
            aliases = p.Aliases,
            path = string.IsNullOrWhiteSpace(p.Path) ? null : p.Path,
            paths = p.Paths,
            howToGetThere = p.HowToGetThere,
            purpose = p.Purpose,
            relatedPlaceIds = p.RelatedPlaceIds,
            commonQuestions = p.CommonQuestions,
            dataSources = p.DataSources,
            audienceNotes = p.AudienceNotes,
            fallbackAnswer = p.FallbackAnswer,
            tips = p.Tips is { Count: > 0 } ? p.Tips : null,
        });
        return JsonSerializer.Serialize(payload, PromptJsonOptions);
    }

    private static HelpFrontendCatalog BuildFrontendCatalog() =>
        new(
            SuggestedQuestions,
            Places.Select(p => new HelpFrontendPlace(
                p.Id,
                p.Title,
                p.Path,
                p.Aliases,
                p.Purpose,
                p.CommonQuestions,
                p.FallbackAnswer)).ToList());

    private static string BuildFrontendCatalogJson() =>
        JsonSerializer.Serialize(BuildFrontendCatalog(), FrontendJsonOptions);
}
