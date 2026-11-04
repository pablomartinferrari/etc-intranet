using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Intranet.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class WorkspaceChatAgentTests
{
    [Fact]
    public async Task ComposeAnswerCitesRepositoryHits()
    {
        await using var db = CreateDb();
        await SeedDoc(db);
        var search = new LocalRepositorySearch(db);
        var document = await db.AgenticRepoDocuments.SingleAsync();
        await search.ReindexDocumentAsync(document, CancellationToken.None);

        var service = new WorkspaceService(
            db,
            new FakeDirectory(),
            search,
            Options.Create(new AgenticOptions { Enabled = true, ShareDemoCatalog = false }),
            new StubEnvironment());

        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = "tester",
            Title = "Alpha",
            RealtimeCollaboration = true,
        };
        var (answer, citations) = await service.ComposeAnswerAsync(chat, "quarterly report", CancellationToken.None);

        Assert.Contains("shared thread", answer, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(citations);
        Assert.Equal("repository", citations[0].Type);
        Assert.Contains("quarterly", citations[0].Snippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendPersistsUserAndAssistantMessages()
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

        var service = new WorkspaceService(
            db,
            new FakeDirectory(),
            search,
            Options.Create(new AgenticOptions { Enabled = true }),
            new StubEnvironment());

        var actor = new WorkspaceActor("tester", "Tester", "tester@etc.local");
        var (user, assistant) = await service.SendAsync(actor, chat.Id, "What does the quarterly report say?", CancellationToken.None);

        Assert.Equal("user", user.Role);
        Assert.Equal("assistant", assistant.Role);
        Assert.Equal(2, db.AgenticChatMessages.Count());
        Assert.NotEqual("New chat", (await db.AgenticChats.SingleAsync()).Title);
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

        public Task<IReadOnlyList<DirectoryPrincipalDto>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DirectoryPrincipalDto>>([]);

        public Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
            string userOid,
            IReadOnlyCollection<string> groupOids,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
