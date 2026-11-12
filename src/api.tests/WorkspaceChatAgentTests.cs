using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Intranet.Api.Data;
using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class WorkspaceChatAgentTests
{
    [Fact]
    public async Task ComposerUsesLlmRouterAndKeepsRepositoryCitations()
    {
        await using var db = CreateDb();
        await SeedDoc(db);
        var search = new LocalRepositorySearch(db);
        await search.ReindexDocumentAsync(await db.AgenticRepoDocuments.SingleAsync(), CancellationToken.None);

        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "LLM answer with the quarterly numbers.");
        var composer = new WorkspaceChatComposer(
            CreateRouter(hosted, ollamaHealthy: false, fallbackKey: "unit-test-placeholder-key"),
            search,
            NullLogger<WorkspaceChatComposer>.Instance);

        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = "tester",
            Title = "Alpha",
            Project = new AgenticProject
            {
                Name = "Alpha",
                Instructions = "Prefer Folder A and keep answers short.",
            },
        };

        var answer = await composer.ComposeAsync(chat, "quarterly report", [], CancellationToken.None);

        Assert.Equal(WorkspaceChatComposer.LlmSource, answer.Source);
        Assert.Equal("LLM answer with the quarterly numbers.", answer.Content);
        Assert.NotEmpty(answer.Citations);
        Assert.Equal("repository", answer.Citations[0].Type);
        Assert.Equal(1, hosted.CallCount);
        Assert.Contains("Prefer Folder A", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("quarterly report", hosted.LastUserPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComposerFallsBackToSearchAndSaysNoModelIsConfigured()
    {
        await using var db = CreateDb();
        await SeedDoc(db);
        var search = new LocalRepositorySearch(db);
        await search.ReindexDocumentAsync(await db.AgenticRepoDocuments.SingleAsync(), CancellationToken.None);
        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "should not run");
        var composer = new WorkspaceChatComposer(
            CreateRouter(hosted, ollamaHealthy: false, fallbackKey: null),
            search,
            NullLogger<WorkspaceChatComposer>.Instance);

        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = "tester",
            Title = "Alpha",
            RealtimeCollaboration = true,
        };
        var answer = await composer.ComposeAsync(chat, "quarterly report", [], CancellationToken.None);

        Assert.Equal(WorkspaceChatComposer.SearchSource, answer.Source);
        Assert.Contains(WorkspaceChatComposer.NoModelMessage, answer.Content, StringComparison.Ordinal);
        Assert.Contains("shared thread", answer.Content, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(answer.Citations);
        Assert.Equal(0, hosted.CallCount);
    }

    [Fact]
    public async Task SendPersistsUserAndAssistantMessagesFromComposer()
    {
        await using var db = CreateDb();
        await SeedDoc(db);
        var search = new LocalRepositorySearch(db);
        await search.ReindexDocumentAsync(await db.AgenticRepoDocuments.SingleAsync(), CancellationToken.None);
        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = "tester",
            Title = "New chat",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.AgenticChats.Add(chat);
        await db.SaveChangesAsync();

        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "Persisted LLM reply.");
        var composer = new WorkspaceChatComposer(
            CreateRouter(hosted, ollamaHealthy: false, fallbackKey: "unit-test-placeholder-key"),
            search,
            NullLogger<WorkspaceChatComposer>.Instance);
        var options = Options.Create(new AgenticOptions { Enabled = true });
        var env = new StubWebEnvironment();
        var service = new WorkspaceService(
            db,
            new FakeDirectory(),
            search,
            new RepositoryReindexService(db, search, options, env, NullLogger<RepositoryReindexService>.Instance),
            composer,
            CreateRouter(hosted, ollamaHealthy: false, fallbackKey: "unit-test-placeholder-key"),
            options,
            env);

        var actor = new WorkspaceActor("tester", "Tester", "tester@etc.local");
        var (user, assistant) = await service.SendAsync(actor, chat.Id, "What does the quarterly report say?", CancellationToken.None);

        Assert.Equal("user", user.Role);
        Assert.Equal("assistant", assistant.Role);
        Assert.Equal("Persisted LLM reply.", assistant.Content);
        Assert.Equal(2, db.AgenticChatMessages.Count());
        Assert.NotEqual("New chat", (await db.AgenticChats.SingleAsync()).Title);
        Assert.Equal(1, hosted.CallCount);
    }

    private static ChatCompletionRouter CreateRouter(
        IChatCompletionClient hosted,
        bool ollamaHealthy,
        string? fallbackKey)
    {
        var fallback = new KnowledgeBaseFallbackOptions { ApiKey = fallbackKey };
        return new ChatCompletionRouter(
            new ScriptedChatClient("ollama", "llama3.1:8b", "local"),
            hosted,
            new ScriptedHealthProbe(ollamaHealthy),
            fallback,
            NullLogger<ChatCompletionRouter>.Instance);
    }

    private static IntranetDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IntranetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IntranetDbContext(options);
    }

    private static async Task SeedDoc(IntranetDbContext db)
    {
        var folder = new AgenticRepoFolder
        {
            Id = Guid.NewGuid(),
            Name = "Folder A",
            Path = "Root / Folder A",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.AgenticRepoFolders.Add(folder);
        db.AgenticRepoDocuments.Add(new AgenticRepoDocument
        {
            Id = Guid.NewGuid(),
            FolderId = folder.Id,
            Folder = folder,
            FolderPath = folder.Path,
            Name = "Document 1",
            FileType = "document",
            MimeType = "text/plain",
            ExtractedText = "The quarterly report covers Alpha pipeline close-out.",
            IsIndexed = false,
            ModifiedAt = DateTimeOffset.UtcNow,
            StoragePath = "doc.txt",
            OwnerName = "ETC",
        });
        await db.SaveChangesAsync();
    }

    private sealed class FakeDirectory : IWorkspaceDirectory
    {
        public IReadOnlyList<DirectoryPrincipalDto> LocalPrincipals => [];
        public bool GraphConfigured => false;

        public Task<IReadOnlyList<DirectoryPrincipalDto>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DirectoryPrincipalDto>>([]);

        public Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
            string userOid,
            IReadOnlyCollection<string> groupOids,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed class StubWebEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ScriptedChatClient : IChatCompletionClient
    {
        private readonly string _content;
        private readonly bool _fail;

        public ScriptedChatClient(string provider, string model, string content = "", bool fail = false)
        {
            ProviderName = provider;
            ModelName = model;
            _content = content;
            _fail = fail;
        }

        public string ProviderName { get; }
        public string ModelName { get; }
        public int CallCount { get; private set; }
        public string LastUserPrompt { get; private set; } = "";

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            CallCount++;
            LastUserPrompt = userPrompt;
            if (_fail)
            {
                throw new InvalidOperationException("scripted failure");
            }

            return Task.FromResult(_content);
        }
    }

    private sealed class ScriptedHealthProbe : IOllamaHealthProbe
    {
        private readonly bool _healthy;

        public ScriptedHealthProbe(bool healthy) => _healthy = healthy;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_healthy);

        public void Invalidate()
        {
        }
    }
}
