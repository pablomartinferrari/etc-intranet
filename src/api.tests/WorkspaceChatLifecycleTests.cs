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

public class WorkspaceChatLifecycleTests
{
    [Fact]
    public async Task OwnerCanRenameChatAndTitleIsTrimmed()
    {
        await using var harness = await Harness.CreateAsync();
        var chat = await harness.Service.CreateChatAsync(
            harness.Actor,
            new CreateChatRequestDto("Draft", null, null, false),
            CancellationToken.None);

        var updated = await harness.Service.UpdateChatAsync(
            harness.Actor,
            chat.Id,
            new UpdateChatRequestDto("  Bid review  ", null, null, null),
            CancellationToken.None);

        Assert.Equal("Bid review", updated.Title);
        var stored = await harness.Db.AgenticChats.AsNoTracking().SingleAsync(c => c.Id == chat.Id);
        Assert.Equal("Bid review", stored.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameRejectsMissingTitle(string? title)
    {
        await using var harness = await Harness.CreateAsync();
        var chat = await harness.Service.CreateChatAsync(
            harness.Actor,
            new CreateChatRequestDto("Draft", null, null, false),
            CancellationToken.None);

        if (title is null)
        {
            var unchanged = await harness.Service.UpdateChatAsync(
                harness.Actor,
                chat.Id,
                new UpdateChatRequestDto(null, null, null, true),
                CancellationToken.None);
            Assert.Equal("Draft", unchanged.Title);
            Assert.True(unchanged.RealtimeCollaboration);
            return;
        }

        var ex = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.UpdateChatAsync(
                harness.Actor,
                chat.Id,
                new UpdateChatRequestDto(title, null, null, null),
                CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenameRejectsTitleOverMaxLength()
    {
        await using var harness = await Harness.CreateAsync();
        var chat = await harness.Service.CreateChatAsync(
            harness.Actor,
            new CreateChatRequestDto("Draft", null, null, false),
            CancellationToken.None);
        var tooLong = new string('a', WorkspaceService.ChatTitleMaxLength + 1);

        var ex = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.UpdateChatAsync(
                harness.Actor,
                chat.Id,
                new UpdateChatRequestDto(tooLong, null, null, null),
                CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("200", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditorCannotRenameOrDeleteChat()
    {
        await using var harness = await Harness.CreateAsync();
        var chat = await harness.ShareWithAsync("editor-oid", "editor");
        var editor = new WorkspaceActor("editor-oid", "Editor", "editor@etc.local");

        var rename = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.UpdateChatAsync(
                editor,
                chat.Id,
                new UpdateChatRequestDto("Hijacked", null, null, null),
                CancellationToken.None));
        Assert.Equal(403, rename.StatusCode);

        var delete = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.DeleteChatAsync(editor, chat.Id, CancellationToken.None));
        Assert.Equal(403, delete.StatusCode);
        Assert.True(await harness.Db.AgenticChats.AnyAsync(c => c.Id == chat.Id));
    }

    [Fact]
    public async Task StrangerRenameOrDeleteReturnsNotFound()
    {
        await using var harness = await Harness.CreateAsync();
        var chat = await harness.Service.CreateChatAsync(
            harness.Actor,
            new CreateChatRequestDto("Secret", null, null, false),
            CancellationToken.None);
        var stranger = new WorkspaceActor("stranger", "Stranger", "stranger@etc.local");

        var rename = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.UpdateChatAsync(
                stranger,
                chat.Id,
                new UpdateChatRequestDto("Nope", null, null, null),
                CancellationToken.None));
        Assert.Equal(404, rename.StatusCode);

        var delete = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.DeleteChatAsync(stranger, chat.Id, CancellationToken.None));
        Assert.Equal(404, delete.StatusCode);
    }

    [Fact]
    public async Task OwnerDeleteRemovesMessagesAndShares()
    {
        await using var harness = await Harness.CreateAsync();
        var folder = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Bids", CancellationToken.None);
        var chat = await harness.Service.CreateChatAsync(
            harness.Actor,
            new CreateChatRequestDto("With history", null, folder.Id, false),
            CancellationToken.None);
        var share = await harness.Service.AddShareAsync(
            harness.Actor,
            chat.Id,
            new CreateShareRequestDto("user", "recipient-oid", "viewer", "Recipient", "recipient@etc.local"),
            CancellationToken.None);
        harness.Db.AgenticChatMessages.Add(new AgenticChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = "user",
            AuthorOid = harness.Actor.Oid,
            AuthorName = harness.Actor.DisplayName,
            Content = "hello",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await harness.Db.SaveChangesAsync();

        await harness.Service.DeleteChatAsync(harness.Actor, chat.Id, CancellationToken.None);

        Assert.False(await harness.Db.AgenticChats.AnyAsync(c => c.Id == chat.Id));
        Assert.False(await harness.Db.AgenticChatMessages.AnyAsync(m => m.ChatId == chat.Id));
        Assert.False(await harness.Db.AgenticChatShares.AnyAsync(s => s.Id == share.Id));
    }

    [Fact]
    public async Task RecipientCanLeaveSharedChatButOwnerCannot()
    {
        await using var harness = await Harness.CreateAsync();
        var chat = await harness.ShareWithAsync("recipient-oid", "viewer");
        var recipient = new WorkspaceActor("recipient-oid", "Recipient", "recipient@etc.local");

        var ownerLeave = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.LeaveChatAsync(harness.Actor, chat.Id, CancellationToken.None));
        Assert.Equal(400, ownerLeave.StatusCode);

        await harness.Service.LeaveChatAsync(recipient, chat.Id, CancellationToken.None);

        Assert.True(await harness.Db.AgenticChats.AnyAsync(c => c.Id == chat.Id));
        Assert.Empty(await harness.Db.AgenticChatShares.Where(s => s.ChatId == chat.Id).ToListAsync());
        var theirSidebar = await harness.Service.GetSidebarAsync(recipient, CancellationToken.None);
        Assert.DoesNotContain(theirSidebar.SharedWithMe, c => c.Id == chat.Id);
        Assert.Null((await harness.Service.GetChatAsync(harness.Actor, chat.Id, CancellationToken.None)).MyShareId);
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
            var rootPath = Path.Combine(Path.GetTempPath(), "ws-chat-life-" + Guid.NewGuid().ToString("N"));
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
            var hosted = new ScriptedChatClient();
            var search = new LocalRepositorySearch(db);
            var options = Options.Create(new AgenticOptions { Enabled = true, RepositoryRoot = rootPath });
            var env = new StubWebEnvironment { ContentRootPath = rootPath };
            var router = new ChatCompletionRouter(
                new ScriptedChatClient(),
                hosted,
                new ScriptedHealthProbe(),
                new KnowledgeBaseFallbackOptions { ApiKey = "unit-test-placeholder-key" },
                NullLogger<ChatCompletionRouter>.Instance);
            var service = new WorkspaceService(
                db,
                new FakeDirectory(),
                search,
                new RepositoryReindexService(db, search, options, env, NullLogger<RepositoryReindexService>.Instance),
                new WorkspaceChatComposer(router, search, NullLogger<WorkspaceChatComposer>.Instance),
                router,
                options,
                env);
            return new Harness { Db = db, Service = service, RootId = root.Id, RootPath = rootPath };
        }

        public async Task<WorkspaceChatDto> ShareWithAsync(string oid, string role)
        {
            var chat = await Service.CreateChatAsync(
                Actor,
                new CreateChatRequestDto("Shared", null, null, false),
                CancellationToken.None);
            await Service.AddShareAsync(
                Actor,
                chat.Id,
                new CreateShareRequestDto("user", oid, role, oid, $"{oid}@etc.local"),
                CancellationToken.None);
            return await Service.GetChatAsync(Actor, chat.Id, CancellationToken.None);
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
        public string ProviderName => "openai";
        public string ModelName => "gpt-4o-mini";

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            Task.FromResult("ok");
    }

    private sealed class ScriptedHealthProbe : IOllamaHealthProbe
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void Invalidate() { }
    }
}
