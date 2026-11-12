using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Intranet.Api.Data;
using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class WorkspaceRepositoryTests
{
    [Fact]
    public void NormalizeRejectsTraversalAndReservedNames()
    {
        Assert.Throws<AgenticException>(() => RepositoryNames.NormalizeFolderName(".."));
        Assert.Throws<AgenticException>(() => RepositoryNames.NormalizeFolderName("a/b"));
        Assert.Throws<AgenticException>(() => RepositoryNames.NormalizeFolderName("a\\b"));
        Assert.Throws<AgenticException>(() => RepositoryNames.NormalizeFolderName("bad:name"));
        Assert.Equal("Bids", RepositoryNames.NormalizeFolderName("  Bids  "));
        Assert.Equal("Root / Bids", RepositoryNames.CombinePath("Root", "Bids"));
    }

    [Fact]
    public async Task CreateRenameMoveAndSearchUseLiveFolderPath()
    {
        await using var harness = await Harness.CreateAsync();
        var child = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Alpha", CancellationToken.None);
        var nested = await harness.Service.CreateFolderAsync(harness.Actor, child.Id, "Nested", CancellationToken.None);
        Assert.Equal("Root / Alpha / Nested", nested.Path);

        var renamed = await harness.Service.RenameFolderAsync(harness.Actor, child.Id, "Bravo", CancellationToken.None);
        Assert.Equal("Root / Bravo", renamed.Path);
        var nestedAfter = await harness.Db.AgenticRepoFolders.AsNoTracking().SingleAsync(f => f.Id == nested.Id);
        Assert.Equal("Root / Bravo / Nested", nestedAfter.Path);

        var other = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Other", CancellationToken.None);
        await harness.Service.MoveFolderAsync(harness.Actor, nested.Id, other.Id, CancellationToken.None);
        var moved = await harness.Db.AgenticRepoFolders.AsNoTracking().SingleAsync(f => f.Id == nested.Id);
        Assert.Equal(other.Id, moved.ParentId);
        Assert.Equal("Root / Other / Nested", moved.Path);
    }

    [Fact]
    public async Task MoveIntoDescendantAndDuplicateNamesAreRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var parent = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Parent", CancellationToken.None);
        var child = await harness.Service.CreateFolderAsync(harness.Actor, parent.Id, "Child", CancellationToken.None);
        await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.MoveFolderAsync(harness.Actor, parent.Id, child.Id, CancellationToken.None));
        var duplicate = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.CreateFolderAsync(harness.Actor, parent.Id, "child", CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task NonEmptyDeleteRequiresRecursiveAndRemovesIndex()
    {
        await using var harness = await Harness.CreateAsync();
        var folder = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Docs", CancellationToken.None);
        await using var stream = new MemoryStream("quarterly report text"u8.ToArray());
        var doc = await harness.Service.UploadAsync(
            harness.Actor,
            folder.Id,
            "report.txt",
            "text/plain",
            stream,
            CancellationToken.None);
        var conflict = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.DeleteFolderAsync(harness.Actor, folder.Id, recursive: false, CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Contains("My chats", conflict.Message, StringComparison.OrdinalIgnoreCase);

        await harness.Service.DeleteFolderAsync(harness.Actor, folder.Id, recursive: true, CancellationToken.None);
        Assert.False(await harness.Db.AgenticRepoDocuments.AnyAsync(d => d.Id == doc.Id));
        Assert.False(await harness.Db.AgenticRepoFolders.AnyAsync(f => f.Id == folder.Id));
    }

    [Fact]
    public void SubtreePrefixMatchesDescendantsOnly()
    {
        Assert.True(RepositoryNames.IsInSubtree("Root / Projects / Hotel Ascend", "Root / Projects / Hotel Ascend"));
        Assert.True(RepositoryNames.IsInSubtree("Root / Projects / Hotel Ascend / Drawings", "Root / Projects / Hotel Ascend"));
        Assert.False(RepositoryNames.IsInSubtree("Root / Projects / Hotel Ascend Extra", "Root / Projects / Hotel Ascend"));
        Assert.False(RepositoryNames.IsInSubtree("Root / Projects", "Root / Projects / Hotel Ascend"));
        Assert.Equal("Root / Projects / Hotel Ascend / ", RepositoryNames.PrefixFilter("Root / Projects / Hotel Ascend"));
    }

    [Fact]
    public async Task SearchIsScopedToFolderSubtreeAndFollowsRenameAndMove()
    {
        await using var harness = await Harness.CreateAsync();
        var hotel = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Hotel Ascend", CancellationToken.None);
        var drawings = await harness.Service.CreateFolderAsync(harness.Actor, hotel.Id, "Drawings", CancellationToken.None);
        var other = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Other", CancellationToken.None);

        await using (var stream = new MemoryStream("hotel elevation drawing"u8.ToArray()))
        {
            await harness.Service.UploadAsync(harness.Actor, drawings.Id, "elevation.txt", "text/plain", stream, CancellationToken.None);
        }

        await using (var stream = new MemoryStream("unrelated warehouse notes"u8.ToArray()))
        {
            await harness.Service.UploadAsync(harness.Actor, other.Id, "warehouse.txt", "text/plain", stream, CancellationToken.None);
        }

        var scoped = await harness.Service.SearchAsync(harness.Actor, "elevation", hotel.Id, [], null, CancellationToken.None);
        Assert.True(scoped.Scoped);
        Assert.Equal("Root / Hotel Ascend", scoped.FolderPath);
        Assert.Contains(scoped.Hits, h => h.Title == "elevation.txt" && h.Path.Contains("Hotel Ascend", StringComparison.Ordinal));
        Assert.DoesNotContain(scoped.Hits, h => h.Title == "warehouse.txt");

        var unscoped = await harness.Service.SearchAsync(harness.Actor, "warehouse", hotel.Id, [], null, CancellationToken.None);
        Assert.Empty(unscoped.Hits);

        await harness.Service.RenameFolderAsync(harness.Actor, hotel.Id, "Hotel Tower", CancellationToken.None);
        var afterRename = await harness.Service.SearchAsync(harness.Actor, "elevation", hotel.Id, [], null, CancellationToken.None);
        Assert.Contains(afterRename.Hits, h => h.Path.Contains("Hotel Tower", StringComparison.Ordinal));
        Assert.DoesNotContain(afterRename.Hits, h => h.Path.Contains("Hotel Ascend", StringComparison.Ordinal));
        var doc = await harness.Db.AgenticRepoDocuments.AsNoTracking().SingleAsync(d => d.Name == "elevation.txt");
        Assert.Contains("Hotel Tower", doc.FolderPath, StringComparison.Ordinal);

        var dest = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Archive", CancellationToken.None);
        await harness.Service.MoveFolderAsync(harness.Actor, drawings.Id, dest.Id, CancellationToken.None);
        var afterMove = await harness.Service.SearchAsync(harness.Actor, "elevation", dest.Id, [], null, CancellationToken.None);
        Assert.Contains(afterMove.Hits, h => h.Path.Contains("Archive", StringComparison.Ordinal) && h.Path.Contains("Drawings", StringComparison.Ordinal));
        var stillInHotel = await harness.Service.SearchAsync(harness.Actor, "elevation", hotel.Id, [], null, CancellationToken.None);
        Assert.Empty(stillInHotel.Hits);
    }

    [Fact]
    public async Task UploadIntoFolderAndSearchShowFolderPath()
    {
        await using var harness = await Harness.CreateAsync();
        var folder = await harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "Finance", CancellationToken.None);
        await using var stream = new MemoryStream("pipeline close-out numbers"u8.ToArray());
        var doc = await harness.Service.UploadAsync(
            harness.Actor,
            folder.Id,
            "numbers.txt",
            "text/plain",
            stream,
            CancellationToken.None);
        Assert.Contains("Finance", doc.Path, StringComparison.Ordinal);

        var hits = await harness.Service.SearchAsync(
            harness.Actor,
            "pipeline",
            folder.Id,
            [],
            null,
            CancellationToken.None);
        Assert.Contains(hits.Hits, h => h.DocumentId == doc.Id && h.Path.Contains("Finance", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RootCannotBeDeletedAndTraversalFileNamesFail()
    {
        await using var harness = await Harness.CreateAsync();
        var ex = await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.DeleteFolderAsync(harness.Actor, harness.RootId, true, CancellationToken.None));
        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
        await Assert.ThrowsAsync<AgenticException>(() =>
            harness.Service.CreateFolderAsync(harness.Actor, harness.RootId, "../etc", CancellationToken.None));
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
            var rootPath = Path.Combine(Path.GetTempPath(), "ws-repo-" + Guid.NewGuid().ToString("N"));
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
            var router = new ChatCompletionRouter(
                hosted,
                hosted,
                new AlwaysDownProbe(),
                new KnowledgeBaseFallbackOptions { ApiKey = "unit-test-placeholder-key" },
                NullLogger<ChatCompletionRouter>.Instance);
            var options = Options.Create(new AgenticOptions { Enabled = true, RepositoryRoot = rootPath });
            var env = new StubWebEnvironment { ContentRootPath = rootPath };
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

    private sealed class AlwaysDownProbe : IOllamaHealthProbe
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void Invalidate() { }
    }
}
