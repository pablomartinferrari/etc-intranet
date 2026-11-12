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
/// <c>/knowledge</c>. Repository search + named/attached document chunks are
/// the RAG context. Prompt token counts are logged; contents are not.
/// </summary>
public sealed class WorkspaceChatComposer : IWorkspaceChatComposer
{
    public const string LlmSource = "llm";
    public const string SearchSource = "search";
    public const int DocumentCharBudget = 24_000;

    public const string NoModelMessage =
        "No language model is configured (Ollama is offline and KnowledgeBase__Fallback__ApiKey is not set), " +
        "so this answer is assembled from repository search only — it is not a generated conversation.";

    internal const string SystemPrompt =
        "You are the ETC intranet Workspace assistant for the signed-in user. " +
        "Answer using the project instructions, attached files, and repository document excerpts provided below. " +
        "Quote or cite documents by title when you use them. " +
        "These documents were uploaded by this user into their own workspace. " +
        "You MUST extract and return personal details that appear in those excerpts " +
        "(phone numbers, emails, addresses, names, employment history) — they belong to the user. " +
        "Do not refuse to read or quote the provided excerpts, and do not claim you cannot access a file that is listed below. " +
        "If a document is marked as having no extractable text, say so and suggest Re-index or a text/DOCX copy. " +
        "Do not invent files, people, or numbers that are not in the excerpts. " +
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
        var emptyTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hits = await _search.SearchAsync(
            new RepoSearchQuery(question, chat.FolderId, emptyTypes, Take: 5),
            cancellationToken);
        var widened = false;
        if (chat.FolderId is not null && LocalRepositorySearch.IsWeak(hits))
        {
            var wider = await _search.SearchAsync(
                new RepoSearchQuery(question, FolderId: null, emptyTypes, Take: 5),
                cancellationToken);
            hits = MergePreferringScoped(hits, wider);
            widened = wider.Count > 0;
        }

        var named = await _search.FindNamedDocumentsAsync(question, chat.FolderId, cancellationToken);
        var attachedIds = new List<Guid>();
        if (chat.DocumentId is { } attachedId)
        {
            attachedIds.Add(attachedId);
        }

        var attached = attachedIds.Count == 0
            ? (IReadOnlyList<RepoDocumentContext>)[]
            : await _search.GetDocumentsAsync(attachedIds, cancellationToken);
        var focused = MergeContexts(attached, named);

        var citations = new List<WorkspaceCitationDto>();
        foreach (var document in focused)
        {
            citations.Add(ToCitation(document));
        }

        foreach (var hit in hits)
        {
            if (citations.Any(c => c.DocumentId == hit.DocumentId))
            {
                continue;
            }

            citations.Add(ToCitation(hit));
        }

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
                RepositoryNames.DocumentPath(document.Folder?.Path ?? document.FolderPath, document.Name),
                $"/workspace/repo/{document.FolderId}?doc={document.Id}"));
        }

        var userPrompt = UserPrompt(chat, question, history, citations, focused, projectFiles, widened);
        _logger.LogInformation(
            "Workspace chat prompt ~{PromptTokens} tokens (system {SystemChars} chars, user {UserChars} chars, {CitationCount} citations, {FocusedCount} named/attached docs). Contents not logged.",
            EstimateTokens(SystemPrompt) + EstimateTokens(userPrompt),
            SystemPrompt.Length,
            userPrompt.Length,
            citations.Count,
            focused.Count);

        var fallback = BuildSearchFallback(chat, citations);
        try
        {
            var result = await _chat.CompleteAsync(SystemPrompt, userPrompt, cancellationToken);
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

    internal static IReadOnlyList<RepoDocumentContext> MergeContexts(
        IReadOnlyList<RepoDocumentContext> attached,
        IReadOnlyList<RepoDocumentContext> named)
    {
        var merged = new List<RepoDocumentContext>();
        var seen = new HashSet<Guid>();
        foreach (var document in attached.Concat(named))
        {
            if (seen.Add(document.DocumentId))
            {
                merged.Add(document);
            }
        }

        return merged;
    }

    internal static string RenderDocumentContents(IReadOnlyList<RepoDocumentContext> documents, int budget = DocumentCharBudget)
    {
        if (documents.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Attached or named document contents (answer from these; they were uploaded by the signed-in user):");
        var remaining = budget;
        foreach (var document in documents)
        {
            sb.AppendLine($"Document: {document.Title} ({document.Path})");
            if (document.ExtractionStatus is RepositoryExtractionStatus.Empty
                or RepositoryExtractionStatus.Unsupported
                or RepositoryExtractionStatus.Error)
            {
                sb.AppendLine($"[no extractable text: {document.ExtractionNote ?? document.ExtractionStatus}]");
                sb.AppendLine();
                continue;
            }

            var chunks = document.Chunks.Count > 0
                ? document.Chunks
                : string.IsNullOrWhiteSpace(document.ExtractedText)
                    ? []
                    : LocalRepositorySearch.Chunk(document.ExtractedText);
            if (chunks.Count == 0)
            {
                sb.AppendLine("[no extractable text]");
                sb.AppendLine();
                continue;
            }

            foreach (var chunk in chunks)
            {
                if (remaining <= 0)
                {
                    sb.AppendLine("[additional chunks omitted to stay within the token budget]");
                    return sb.ToString();
                }

                var take = chunk.Length <= remaining ? chunk : chunk[..remaining];
                sb.AppendLine(take);
                remaining -= take.Length;
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    internal static int EstimateTokens(string text) => Math.Max(1, (text ?? "").Length / 4);

    private static WorkspaceCitationDto ToCitation(RepoSearchHit hit) =>
        new(
            "repository",
            hit.Title,
            Trim(hit.Snippet, 1200) ?? "",
            hit.DocumentId,
            hit.Path,
            $"/workspace/repo/{hit.FolderId}?doc={hit.DocumentId}");

    private static WorkspaceCitationDto ToCitation(RepoDocumentContext document)
    {
        var snippet = document.Chunks.Count > 0
            ? document.Chunks[0]
            : document.ExtractedText;
        if (string.IsNullOrWhiteSpace(snippet))
        {
            snippet = document.ExtractionNote ?? "No extractable text.";
        }

        return new WorkspaceCitationDto(
            "repository",
            document.Title,
            Trim(snippet, 1200) ?? "",
            document.DocumentId,
            document.Path,
            $"/workspace/repo/{document.FolderId}?doc={document.DocumentId}");
    }

    private static string UserPrompt(
        AgenticChat chat,
        string question,
        IReadOnlyList<AgenticChatMessage> history,
        IReadOnlyList<WorkspaceCitationDto> citations,
        IReadOnlyList<RepoDocumentContext> focused,
        IReadOnlyList<AgenticRepoDocument> projectFiles,
        bool widenedFromFolderScope)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(chat.Project?.Name))
        {
            sb.AppendLine($"Project: {chat.Project.Name}");
        }

        if (chat.Folder is not null)
        {
            sb.AppendLine($"Repository folder: {chat.Folder.Path}");
            sb.AppendLine("Prefer files in this folder and its subfolders.");
            if (widenedFromFolderScope)
            {
                sb.AppendLine("Folder search was weak; additional excerpts below come from the rest of the repository.");
            }
        }

        if (chat.Document is not null)
        {
            sb.AppendLine($"This chat is attached to the file: {chat.Document.Name}.");
            sb.AppendLine("Use that file's contents first.");
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

        var contents = RenderDocumentContents(focused);
        if (!string.IsNullOrWhiteSpace(contents))
        {
            sb.AppendLine(contents);
        }

        var extra = citations.Where(c => focused.All(d => d.DocumentId != c.DocumentId)).ToList();
        if (extra.Count > 0)
        {
            sb.AppendLine("Additional repository excerpts (cite these when used):");
            foreach (var citation in extra)
            {
                sb.AppendLine($"- {citation.Title} ({citation.Path}): {citation.Snippet}");
            }

            sb.AppendLine();
        }
        else if (focused.Count == 0)
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

    internal static IReadOnlyList<RepoSearchHit> MergePreferringScoped(
        IReadOnlyList<RepoSearchHit> scoped,
        IReadOnlyList<RepoSearchHit> wider)
    {
        var merged = new List<RepoSearchHit>(scoped);
        var seen = new HashSet<Guid>(scoped.Select(h => h.DocumentId));
        foreach (var hit in wider)
        {
            if (seen.Add(hit.DocumentId))
            {
                merged.Add(hit);
            }
        }

        return merged.Take(5).ToList();
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? value : value.Length <= max ? value : value[..max] + "…";
}
