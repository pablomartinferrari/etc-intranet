using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Intranet.Api.Data;
using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class WorkspaceChatScopeTests
{
    [Fact]
    public async Task SidebarMyChatsExcludesFolderAndProjectScopedChats()
    {
        await using var harness = await Harness.CreateAsync();
        var actor = harness.Actor;
        var unscoped = await harness.Service.CreateChatAsync(actor, new CreateChatRequestDto("Loose", null, null, false), CancellationToken.None);
        var project = await harness.Service.CreateProjectAsync(actor, new CreateProjectRequestDto("Alpha", null), CancellationToken.None);
        var inProject = await harness.Service.CreateChatAsync(actor, new CreateChatRequestDto("Project chat", project.Id, null, false), CancellationToken.None);
        var folder = await harness.Service.CreateFolderAsync(actor, harness.RootId, "Bids", CancellationToken.None);
        var inFolder = await harness.Service.CreateChatAsync(actor, new CreateChatRequestDto("Folder chat", null, folder.Id, false), CancellationToken.None);

        var sidebar = await harness.Service.GetSidebarAsync(actor, CancellationToken.None);

        Assert.Contains(sidebar.MyChats, c => c.Id == unscoped.Id);
        Assert.DoesNotContain(sidebar.MyChats, c => c.Id == inProject.Id);
        Assert.DoesNotContain(sidebar.MyChats, c => c.Id == inFolder.Id);
        Assert.Contains(sidebar.ScopedChats, c => c.Id == inProject.Id && c.ProjectId == project.Id);
        Assert.Contains(sidebar.ScopedChats, c => c.Id == inFolder.Id && c.FolderId == folder.Id && c.FolderPath!.Contains("Bids", StringComparison.Ordinal));
        Assert.Equal(1, sidebar.Repository.Single(f => f.Id == folder.Id).ChatCount);
        Assert.Equal(1, sidebar.Projects.Single(p => p.Id == project.Id).ChatCount);
    }

    [Fact]
    public async Task CreateChatPersistsFolderAndProjectScope()
    {
        await using var harness = await Harness.CreateAsync();
        var project = await harness.Service.CreateProjectAsync(harness.Actor, new CreateProjectRequestDto("Alpha", null), CancellationToken.None);
        var folder = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Legal", CancellationToken.None);

        var chat = await harness.Service.CreateChatAsync(
            harness.Actor,
            new CreateChatRequestDto("Scoped", project.Id, folder.Id, false),
            CancellationToken.None);

        var stored = await harness.Db.AgenticChats.AsNoTracking().SingleAsync(c => c.Id == chat.Id);
        Assert.Equal(project.Id, stored.ProjectId);
        Assert.Equal(folder.Id, stored.FolderId);
        Assert.Equal(folder.Id, chat.FolderId);
        Assert.Equal(project.Id, chat.ProjectId);
        Assert.Contains("Legal", chat.FolderPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharedChatKeepsFolderPathForRecipient()
    {
        await using var harness = await Harness.CreateAsync();
        var folder = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Shared folder", CancellationToken.None);
        var chat = await harness.Service.CreateChatAsync(
            harness.Actor,
            new CreateChatRequestDto("Shared scoped", null, folder.Id, false),
            CancellationToken.None);
        await harness.Service.AddShareAsync(
            harness.Actor,
            chat.Id,
            new CreateShareRequestDto("user", "recipient-oid", "viewer", "Recipient", "recipient@etc.local"),
            CancellationToken.None);

        var ownerSidebar = await harness.Service.GetSidebarAsync(harness.Actor, CancellationToken.None);
        Assert.Contains(ownerSidebar.SharedByMe, c => c.Id == chat.Id && c.FolderPath!.Contains("Shared folder", StringComparison.Ordinal));
        Assert.DoesNotContain(ownerSidebar.MyChats, c => c.Id == chat.Id);

        var recipient = new WorkspaceActor("recipient-oid", "Recipient", "recipient@etc.local");
        var theirSidebar = await harness.Service.GetSidebarAsync(recipient, CancellationToken.None);
        Assert.Contains(theirSidebar.SharedWithMe, c => c.Id == chat.Id && c.FolderPath!.Contains("Shared folder", StringComparison.Ordinal));
        Assert.Empty(theirSidebar.MyChats);
    }

    [Fact]
    public async Task DeletingFolderMovesChatsToMyChatsAndKeepsThemOnRenameAndMove()
    {
        await using var harness = await Harness.CreateAsync();
        var parent = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Parent", CancellationToken.None);
        var folder = await harness.Service.CreateFolderAsync(harness.Actor, parent.Id, "Nested", CancellationToken.None);
        var chat = await harness.Service.CreateChatAsync(
            harness.Actor,
            new CreateChatRequestDto("Stays attached", null, folder.Id, false),
            CancellationToken.None);

        await harness.Service.RenameFolderAsync(harness.Actor, folder.Id, "Renamed", CancellationToken.None);
        var afterRename = await harness.Service.GetChatAsync(harness.Actor, chat.Id, CancellationToken.None);
        Assert.Equal(folder.Id, afterRename.FolderId);
        Assert.Contains("Renamed", afterRename.FolderPath, StringComparison.Ordinal);

        var sibling = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Other", CancellationToken.None);
        await harness.Service.MoveFolderAsync(harness.Actor, folder.Id, sibling.Id, CancellationToken.None);
        var afterMove = await harness.Service.GetChatAsync(harness.Actor, chat.Id, CancellationToken.None);
        Assert.Equal(folder.Id, afterMove.FolderId);
        Assert.Contains("Other", afterMove.FolderPath, StringComparison.Ordinal);
        Assert.Contains("Renamed", afterMove.FolderPath, StringComparison.Ordinal);

        await harness.Service.DeleteFolderAsync(harness.Actor, folder.Id, recursive: false, CancellationToken.None);
        var stored = await harness.Db.AgenticChats.AsNoTracking().SingleAsync(c => c.Id == chat.Id);
        Assert.Null(stored.FolderId);
        var sidebar = await harness.Service.GetSidebarAsync(harness.Actor, CancellationToken.None);
        Assert.Contains(sidebar.MyChats, c => c.Id == chat.Id);
        Assert.DoesNotContain(sidebar.ScopedChats, c => c.Id == chat.Id);
    }

    [Fact]
    public async Task ComposerPromptIncludesFolderPath()
    {
        await using var harness = await Harness.CreateAsync();
        var folder = new AgenticRepoFolder
        {
            Id = Guid.NewGuid(),
            ParentId = harness.RootId,
            Name = "Folder A",
            Path = "Root / Folder A",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        harness.Db.AgenticRepoFolders.Add(folder);
        await harness.Db.SaveChangesAsync();
        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "ok");
        var composer = new WorkspaceChatComposer(
            CreateRouter(hosted),
            new LocalRepositorySearch(harness.Db),
            NullLogger<WorkspaceChatComposer>.Instance);
        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = harness.Actor.Oid,
            Title = "Folder chat",
            FolderId = folder.Id,
            Folder = folder,
        };

        await composer.ComposeAsync(chat, "what is in here", [], CancellationToken.None);

        Assert.Contains("Root / Folder A", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("this folder and its subfolders only", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("scoped to the repository folder 'Root / Folder A'", hosted.LastSystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Folder search was weak", hosted.LastUserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComposerDoesNotFallBackToWholeRepoWhenFolderSearchIsWeak()
    {
        var (harness, hotel, _, composer, hosted) = await FolderPairAsync();
        await using var _ = harness;
        var folder = await harness.Db.AgenticRepoFolders.AsNoTracking().SingleAsync(f => f.Id == hotel);
        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = harness.Actor.Oid,
            Title = "Hotel chat",
            FolderId = hotel,
            Folder = folder,
        };

        var answer = await composer.ComposeAsync(chat, "pallet count", [], CancellationToken.None);

        Assert.DoesNotContain("warehouse.txt", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("warehouse pallet count", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Folder search was weak", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("Root / Hotel", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("No matching documents in Root / Hotel", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("do not pull from the rest of the repository", hosted.LastSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(answer.Citations, c => (c.Path ?? "").Contains("warehouse", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ComposerIncludesNamedFileOutsideFolder()
    {
        var (harness, hotel, other, composer, hosted) = await FolderPairAsync();
        await using var _ = harness;
        var folder = await harness.Db.AgenticRepoFolders.AsNoTracking().SingleAsync(f => f.Id == hotel);
        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = harness.Actor.Oid,
            Title = "Hotel chat",
            FolderId = hotel,
            Folder = folder,
        };

        var answer = await composer.ComposeAsync(chat, "what does warehouse.txt say?", [], CancellationToken.None);

        Assert.Contains("warehouse.txt", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("warehouse pallet count", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("Root / Other", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("folder path:", hosted.LastUserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(answer.Citations, c =>
            c.Title == "warehouse.txt" && (c.Path ?? "").Contains("Root / Other", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ComposerIncludesAttachedFileOutsideFolder()
    {
        var (harness, hotel, other, composer, hosted) = await FolderPairAsync();
        await using var _ = harness;
        var warehouse = await harness.Db.AgenticRepoDocuments.AsNoTracking().SingleAsync(d => d.FolderId == other);
        var folder = await harness.Db.AgenticRepoFolders.AsNoTracking().SingleAsync(f => f.Id == hotel);
        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = harness.Actor.Oid,
            Title = "Hotel chat",
            FolderId = hotel,
            Folder = folder,
            DocumentId = warehouse.Id,
            Document = await harness.Db.AgenticRepoDocuments.SingleAsync(d => d.Id == warehouse.Id),
        };

        await composer.ComposeAsync(chat, "what is the pallet count?", [], CancellationToken.None);

        Assert.Contains("warehouse pallet count", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("attached to the file: warehouse.txt", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("even if it is outside the folder scope", hosted.LastUserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComposerMyChatsSearchesWholeRepository()
    {
        var (harness, _, _, composer, hosted) = await FolderPairAsync();
        await using var _ = harness;
        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = harness.Actor.Oid,
            Title = "Loose",
        };

        var answer = await composer.ComposeAsync(chat, "pallet count", [], CancellationToken.None);

        Assert.Contains("whole repository (My chats)", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("warehouse.txt", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("warehouse pallet count", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("My chats", hosted.LastSystemPrompt, StringComparison.Ordinal);
        Assert.Contains(answer.Citations, c =>
            c.Title == "warehouse.txt" && (c.Path ?? "").Contains("Root / Other", StringComparison.Ordinal));
    }

    private static async Task<(Harness Harness, Guid HotelId, Guid OtherId, WorkspaceChatComposer Composer, ScriptedChatClient Hosted)> FolderPairAsync()
    {
        var harness = await Harness.CreateAsync();
        var hotel = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Hotel", CancellationToken.None);
        var other = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Other", CancellationToken.None);
        await using (var stream = new MemoryStream("only hotel finishes"u8.ToArray()))
        {
            await harness.Service.UploadAsync(harness.Actor, hotel.Id, "finishes.txt", "text/plain", stream, CancellationToken.None);
        }

        await using (var stream = new MemoryStream("warehouse pallet count"u8.ToArray()))
        {
            await harness.Service.UploadAsync(harness.Actor, other.Id, "warehouse.txt", "text/plain", stream, CancellationToken.None);
        }

        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "ok");
        var composer = new WorkspaceChatComposer(
            CreateRouter(hosted),
            new LocalRepositorySearch(harness.Db),
            NullLogger<WorkspaceChatComposer>.Instance);
        return (harness, hotel.Id, other.Id, composer, hosted);
    }

    private static ChatCompletionRouter CreateRouter(IChatCompletionClient hosted)
    {
        return new ChatCompletionRouter(
            new ScriptedChatClient("ollama", "llama3.1:8b", "local"),
            hosted,
            new ScriptedHealthProbe(false),
            new KnowledgeBaseFallbackOptions { ApiKey = "unit-test-placeholder-key" },
            NullLogger<ChatCompletionRouter>.Instance);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required IntranetDbContext Db { get; init; }
        public required WorkspaceService Service { get; init; }
        public required Guid RootId { get; init; }
        public required string RootPath { get; init; }
        public WorkspaceActor Actor { get; } = new("tester", "Tester", "tester@etc.local");

        public static async Task<Harness> CreateAsync()
        {
            var db = new IntranetDbContext(new DbContextOptionsBuilder<IntranetDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            var rootPath = Path.Combine(Path.GetTempPath(), "ws-scope-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            var root = new AgenticRepoFolder
            {
                Id = Guid.NewGuid(),
                Name = "Root",
                Path = "Root",
                IsPublic = true,
                IsIndexed = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.AgenticRepoFolders.Add(root);
            await db.SaveChangesAsync();
            var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "ok");
            var search = new LocalRepositorySearch(db);
            var options = Options.Create(new AgenticOptions { Enabled = true, RepositoryRoot = rootPath });
            var env = new StubWebEnvironment { ContentRootPath = rootPath };
            var service = new WorkspaceService(
                db,
                new FakeDirectory(),
                search,
                new RepositoryReindexService(db, search, options, env, NullLogger<RepositoryReindexService>.Instance),
                new WorkspaceChatComposer(CreateRouter(hosted), search, NullLogger<WorkspaceChatComposer>.Instance),
                CreateRouter(hosted),
                options,
                env);
            return new Harness { Db = db, Service = service, RootId = root.Id, RootPath = rootPath };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
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
        public ScriptedChatClient(string provider, string model, string content)
        {
            ProviderName = provider;
            ModelName = model;
            Content = content;
        }

        public string ProviderName { get; }
        public string ModelName { get; }
        public string Content { get; }
        public string LastUserPrompt { get; private set; } = "";
        public string LastSystemPrompt { get; private set; } = "";

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            LastSystemPrompt = systemPrompt;
            LastUserPrompt = userPrompt;
            return Task.FromResult(Content);
        }
    }

    private sealed class ScriptedHealthProbe : IOllamaHealthProbe
    {
        private readonly bool _healthy;
        public ScriptedHealthProbe(bool healthy) => _healthy = healthy;
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(_healthy);
        public void Invalidate() { }
    }
}
