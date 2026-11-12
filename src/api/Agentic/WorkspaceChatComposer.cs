using System.Text;
using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Intranet.Api.KnowledgeBase.Services;

namespace Intranet.Api.Agentic;

public sealed record WorkspaceChatAnswer(
    string Content,
    IReadOnlyList<WorkspaceCitationDto> Citations,
    string Source);

public interface IWorkspaceChatComposer
{
    Task<WorkspaceChatAnswer> ComposeAsync(
        AgenticChat chat,
        string question,
        IReadOnlyList<AgenticChatMessage> history,
        CancellationToken cancellationToken);
}

/// <summary>
/// Workspace assistant: same Ollama → hosted Azure OpenAI / OpenAI router as
/// <c>/knowledge</c>. Repository search + project files are cited RAG context.
/// <c>/knowledge</c> uses non-streaming <see cref="ChatCompletionRouter.CompleteAsync"/>,
/// so this path matches (SignalR still broadcasts the finished message).
/// </summary>
public sealed class WorkspaceChatComposer : IWorkspaceChatComposer
{
    public const string LlmSource = "llm";
    public const string SearchSource = "search";

    public const string NoModelMessage =
        "No language model is configured (Ollama is offline and KnowledgeBase__Fallback__ApiKey is not set), " +
        "so this answer is assembled from repository search only — it is not a generated conversation.";

    private const string SystemPrompt =
        "You are the ETC intranet Workspace assistant for the signed-in user. " +
        "Use only the project instructions, project files, repository excerpts, and chat history provided. " +
        "Cite repository documents by title when you use them. Do not invent files, people, or numbers. " +
        "If the excerpts are not enough, say so and suggest uploading a file or refining the question. " +
        "This is not the /knowledge Chat app; you only have this workspace's repository and project context.";

    private readonly ChatCompletionRouter _chat;
    private readonly IRepositorySearch _search;
    private readonly ILogger<WorkspaceChatComposer> _logger;

    public WorkspaceChatComposer(
        ChatCompletionRouter chat,
        IRepositorySearch search,
        ILogger<WorkspaceChatComposer> logger)
    {
        _chat = chat;
        _search = search;
        _logger = logger;
    }

    public async Task<WorkspaceChatAnswer> ComposeAsync(
        AgenticChat chat,
        string question,
        IReadOnlyList<AgenticChatMessage> history,
        CancellationToken cancellationToken)
    {
        var hits = await _search.SearchAsync(
            new RepoSearchQuery(question, null, new HashSet<string>(StringComparer.OrdinalIgnoreCase), Take: 5),
            cancellationToken);
        var citations = hits.Select(ToCitation).ToList();
        var projectFiles = chat.Project?.Files
            .Select(f => f.Document)
            .Where(d => d is not null)
            .Cast<AgenticRepoDocument>()
            .ToList() ?? [];
        foreach (var document in projectFiles)
        {
            if (citations.Any(c => c.DocumentId == document.Id))
            {
                continue;
            }

            var snippet = Trim(document.ExtractedText, 280);
            if (string.IsNullOrWhiteSpace(snippet))
            {
                continue;
            }

            citations.Add(new WorkspaceCitationDto(
                "repository",
                document.Name,
                snippet,
                document.Id,
                document.Folder?.Path,
                $"/workspace/repo/{document.FolderId}?doc={document.Id}"));
        }

        var fallback = BuildSearchFallback(chat, citations);
        try
        {
            var result = await _chat.CompleteAsync(
                SystemPrompt,
                UserPrompt(chat, question, history, citations, projectFiles),
                cancellationToken);
            var text = result.Content?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return new WorkspaceChatAnswer(fallback, citations, SearchSource);
            }

            return new WorkspaceChatAnswer(text, citations, LlmSource);
        }
        catch (ChatUnavailableException ex)
        {
            _logger.LogInformation("Workspace chat using search fallback: {Message}", ex.Message);
            return new WorkspaceChatAnswer(fallback, citations, SearchSource);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Workspace LLM failed; using repository search fallback.");
            return new WorkspaceChatAnswer(fallback, citations, SearchSource);
        }
    }

    public static string BuildSearchFallback(AgenticChat chat, IReadOnlyList<WorkspaceCitationDto> citations)
    {
        var sb = new StringBuilder();
        sb.AppendLine(NoModelMessage);
        sb.AppendLine();
        if (chat.RealtimeCollaboration)
        {
            sb.AppendLine("This is a shared thread — the answer is for everyone in the chat.");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(chat.Project?.Instructions))
        {
            sb.AppendLine($"Project instructions: {Trim(chat.Project.Instructions, 240)}");
            sb.AppendLine();
        }

        if (citations.Count == 0)
        {
            sb.Append("I do not have a matching repository document yet. Upload a file or start a project and try again.");
            return sb.ToString();
        }

        sb.AppendLine("I found this in the repository:");
        sb.AppendLine();
        for (var i = 0; i < citations.Count; i++)
        {
            var c = citations[i];
            sb.AppendLine($"{i + 1}. **{c.Title}** — {c.Snippet}");
        }

        return sb.ToString().TrimEnd();
    }

    private static WorkspaceCitationDto ToCitation(RepoSearchHit hit) =>
        new(
            "repository",
            hit.Title,
            hit.Snippet,
            hit.DocumentId,
            hit.Path,
            $"/workspace/repo/{hit.FolderId}?doc={hit.DocumentId}");

    private static string UserPrompt(
        AgenticChat chat,
        string question,
        IReadOnlyList<AgenticChatMessage> history,
        IReadOnlyList<WorkspaceCitationDto> citations,
        IReadOnlyList<AgenticRepoDocument> projectFiles)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(chat.Project?.Name))
        {
            sb.AppendLine($"Project: {chat.Project.Name}");
        }

        if (!string.IsNullOrWhiteSpace(chat.Project?.Instructions))
        {
            sb.AppendLine("Project instructions:");
            sb.AppendLine(Trim(chat.Project.Instructions, 4000));
            sb.AppendLine();
        }

        if (projectFiles.Count > 0)
        {
            sb.AppendLine("Linked project files:");
            foreach (var file in projectFiles.Take(8))
            {
                sb.AppendLine($"- {file.Name}: {Trim(file.ExtractedText, 600)}");
            }

            sb.AppendLine();
        }

        if (citations.Count > 0)
        {
            sb.AppendLine("Repository search excerpts (cite these when used):");
            foreach (var citation in citations)
            {
                sb.AppendLine($"- {citation.Title} ({citation.Path}): {citation.Snippet}");
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("No repository search hits.");
            sb.AppendLine();
        }

        var recent = history
            .Where(m => m.Role is "user" or "assistant")
            .TakeLast(8)
            .ToList();
        if (recent.Count > 0)
        {
            sb.AppendLine("Recent thread:");
            foreach (var message in recent)
            {
                sb.AppendLine($"{message.Role}: {Trim(message.Content, 500)}");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"Question: {question}");
        return sb.ToString();
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? value : value.Length <= max ? value : value[..max] + "…";
}
