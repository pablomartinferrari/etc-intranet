using System.Text;
using System.Text.Json;
using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Data.Entities;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.Services;

public sealed class RagService
{
    private readonly SemanticSearchService _search;
    private readonly OllamaClient _ollama;
    private readonly WebSearchService _webSearch;
    private readonly ChatSearchRouter _router;
    private readonly ChatExportService _export;
    private readonly KnowledgeDbContext _db;
    private readonly KnowledgeBaseOptions _options;

    public RagService(
        SemanticSearchService search,
        OllamaClient ollama,
        WebSearchService webSearch,
        ChatSearchRouter router,
        ChatExportService export,
        KnowledgeDbContext db,
        IOptions<KnowledgeBaseOptions> options)
    {
        _search = search;
        _ollama = ollama;
        _webSearch = webSearch;
        _router = router;
        _export = export;
        _db = db;
        _options = options.Value;
    }

    public ChatCapabilitiesDto GetCapabilities() =>
        new(
            _webSearch.IsAvailable,
            _webSearch.IsAvailable
                ? ["auto", "documents", "web", "both"]
                : ["documents"],
            true,
            ["xlsx", "docx"]);

    public async Task<ChatResponseDto> ChatAsync(
        ChatRequestDto request,
        string? userOid,
        CancellationToken cancellationToken = default)
    {
        var session = await EnsureSessionAsync(
            request.SessionId,
            userOid,
            request.ProjectId,
            request.Query,
            cancellationToken);

        var useDocuments = !string.Equals(request.SearchMode, "web", StringComparison.OrdinalIgnoreCase);
        var chunks = useDocuments
            ? await _search.RetrieveChunksAsync(
                request.Query,
                _options.ChatTopK,
                request.DocumentId,
                request.ProjectId,
                cancellationToken)
            : [];

        var bestDocScore = chunks.Count > 0 ? chunks.Max(c => c.Score) : 0;
        var resolved = _router.Resolve(request.SearchMode, chunks.Count > 0, bestDocScore);

        if (resolved is ResolvedSearchMode.Web or ResolvedSearchMode.Both && !_webSearch.IsAvailable)
        {
            const string webDisabled =
                "Web search is not configured on the server. " +
                "Set WebSearch:Enabled and WebSearch:ApiKey (Tavily) in API settings, or use document search.";
            await SaveMessageAsync(session.Id, "user", request.Query, null, cancellationToken: cancellationToken);
            await SaveMessageAsync(session.Id, "assistant", webDisabled, [], cancellationToken: cancellationToken);
            await TouchSessionAsync(session, cancellationToken);
            return new ChatResponseDto(session.Id, webDisabled, [], "none", []);
        }

        IReadOnlyList<WebSearchResult> webResults = [];
        if (resolved is ResolvedSearchMode.Web or ResolvedSearchMode.Both)
        {
            webResults = await _webSearch.SearchAsync(request.Query, cancellationToken);
        }

        if (chunks.Count == 0 && webResults.Count == 0)
        {
            const string noContextAnswer =
                "I could not find relevant project documents or web results for that question. " +
                "Try uploading files, enabling web search, or rephrasing your query.";
            await SaveMessageAsync(session.Id, "user", request.Query, null, cancellationToken: cancellationToken);
            await SaveMessageAsync(session.Id, "assistant", noContextAnswer, [], cancellationToken: cancellationToken);
            await TouchSessionAsync(session, cancellationToken);
            return new ChatResponseDto(session.Id, noContextAnswer, [], "none", []);
        }

        var citations = BuildCitations(chunks, webResults, resolved);
        var contextBuilder = BuildContext(chunks, webResults, resolved);
        var systemPrompt = await BuildSystemPromptAsync(request.ProjectId, resolved, cancellationToken);
        var sourcesUsed = resolved switch
        {
            ResolvedSearchMode.Web => "web",
            ResolvedSearchMode.Both => "both",
            _ => "documents",
        };

        await SaveMessageAsync(session.Id, "user", request.Query, null, cancellationToken: cancellationToken);

        var exportFormat = ChatExportIntent.Detect(request.Query);
        if (exportFormat is not null)
        {
            try
            {
                var assistantId = Guid.NewGuid();
                var built = await _export.BuildExportAsync(
                    exportFormat.Value,
                    request.Query,
                    contextBuilder.ToString(),
                    systemPrompt,
                    cancellationToken);

                await SaveMessageAsync(
                    session.Id,
                    "assistant",
                    built.Answer,
                    citations,
                    assistantId,
                    cancellationToken);

                var attachment = await _export.SaveExportAsync(
                    built,
                    session.Id,
                    assistantId,
                    userOid,
                    request.ProjectId,
                    cancellationToken);

                await TouchSessionAsync(session, cancellationToken);
                return new ChatResponseDto(session.Id, built.Answer, citations, sourcesUsed, [attachment]);
            }
            catch (Exception ex)
            {
                var failAnswer =
                    $"I could not generate the {ChatExportIntent.FormatLabel(exportFormat.Value)} file: {ex.Message}. " +
                    "Try asking for a simpler table or document, or rephrase your request.";
                await SaveMessageAsync(session.Id, "assistant", failAnswer, citations, cancellationToken: cancellationToken);
                await TouchSessionAsync(session, cancellationToken);
                return new ChatResponseDto(session.Id, failAnswer, citations, sourcesUsed, []);
            }
        }

        var userPrompt = $"""
            Context:
            {contextBuilder}

            Question: {request.Query}
            """;

        var answer = await _ollama.ChatAsync(systemPrompt, userPrompt, cancellationToken);

        await SaveMessageAsync(session.Id, "assistant", answer, citations, cancellationToken: cancellationToken);
        await TouchSessionAsync(session, cancellationToken);

        return new ChatResponseDto(session.Id, answer, citations, sourcesUsed, []);
    }

    private static IReadOnlyList<CitationDto> BuildCitations(
        IReadOnlyList<(Guid ChunkId, Guid DocumentId, string Title, string? SourceUri, string Text, double Score)> chunks,
        IReadOnlyList<WebSearchResult> webResults,
        ResolvedSearchMode resolved)
    {
        var citations = new List<CitationDto>();

        if (resolved is ResolvedSearchMode.Documents or ResolvedSearchMode.Both)
        {
            citations.AddRange(
                chunks
                    .GroupBy(c => c.DocumentId)
                    .Select(g =>
                    {
                        var first = g.OrderByDescending(x => x.Score).First();
                        return new CitationDto(
                            "document",
                            first.Title,
                            Truncate(first.Text, 400),
                            first.DocumentId,
                            first.SourceUri,
                            null);
                    }));
        }

        if (resolved is ResolvedSearchMode.Web or ResolvedSearchMode.Both)
        {
            citations.AddRange(
                webResults.Select(w => new CitationDto(
                    "web",
                    w.Title,
                    w.Snippet,
                    null,
                    null,
                    w.Url)));
        }

        return citations;
    }

    private static StringBuilder BuildContext(
        IReadOnlyList<(Guid ChunkId, Guid DocumentId, string Title, string? SourceUri, string Text, double Score)> chunks,
        IReadOnlyList<WebSearchResult> webResults,
        ResolvedSearchMode resolved)
    {
        var contextBuilder = new StringBuilder();
        var sourceIndex = 1;

        if (resolved is ResolvedSearchMode.Documents or ResolvedSearchMode.Both)
        {
            foreach (var chunk in chunks)
            {
                contextBuilder.AppendLine($"[Document {sourceIndex}: {chunk.Title}]");
                contextBuilder.AppendLine(chunk.Text);
                contextBuilder.AppendLine();
                sourceIndex++;
            }
        }

        if (resolved is ResolvedSearchMode.Web or ResolvedSearchMode.Both)
        {
            foreach (var result in webResults)
            {
                contextBuilder.AppendLine($"[Web {sourceIndex}: {result.Title} ({result.Url})]");
                contextBuilder.AppendLine(result.Snippet);
                contextBuilder.AppendLine();
                sourceIndex++;
            }
        }

        return contextBuilder;
    }

    private async Task<string> BuildSystemPromptAsync(
        Guid? projectId,
        ResolvedSearchMode resolved,
        CancellationToken cancellationToken)
    {
        var sourceRule = resolved switch
        {
            ResolvedSearchMode.Web =>
                "Answer using ONLY the provided web search excerpts. Cite page titles and URLs.",
            ResolvedSearchMode.Both =>
                "Answer using the provided internal documents and web excerpts. " +
                "Clearly distinguish company documents from external web sources. Cite both.",
            _ =>
                "Answer using ONLY the provided internal document context. " +
                "If the context is insufficient, say so clearly. Cite document titles.",
        };

        const string basePrompt = """
            You are a helpful assistant for a company knowledge base.
            Be concise and practical.
            """;

        var defaultPrompt = $"{basePrompt}\n{sourceRule}";

        if (!projectId.HasValue)
        {
            return defaultPrompt;
        }

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId.Value, cancellationToken);

        if (project is null || string.IsNullOrWhiteSpace(project.Instructions))
        {
            return defaultPrompt;
        }

        return $"""
            {defaultPrompt}

            Additional project instructions:
            {project.Instructions.Trim()}
            """;
    }

    private async Task<KbChatSession> EnsureSessionAsync(
        Guid? sessionId,
        string? userOid,
        Guid? projectId,
        string firstQuery,
        CancellationToken cancellationToken)
    {
        if (sessionId.HasValue)
        {
            var existing = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId.Value, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        var now = DateTimeOffset.UtcNow;
        var session = new KbChatSession
        {
            Id = Guid.NewGuid(),
            UserOid = userOid,
            ProjectId = projectId,
            Title = Truncate(firstQuery, 80),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task TouchSessionAsync(KbChatSession session, CancellationToken cancellationToken)
    {
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> SaveMessageAsync(
        Guid sessionId,
        string role,
        string content,
        IReadOnlyList<CitationDto>? citations,
        Guid? messageId = null,
        CancellationToken cancellationToken = default)
    {
        var id = messageId ?? Guid.NewGuid();
        _db.ChatMessages.Add(new KbChatMessage
        {
            Id = id,
            SessionId = sessionId,
            Role = role,
            Content = content,
            CitationsJson = citations is null ? null : JsonSerializer.Serialize(citations),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
        return id;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "...";
}
