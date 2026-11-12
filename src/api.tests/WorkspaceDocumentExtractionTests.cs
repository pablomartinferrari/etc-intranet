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

public class WorkspaceDocumentExtractionTests
{
    [Fact]
    public void PdfExtractionIncludesPhoneNumber()
    {
        var pdf = WorkspaceDocumentFixtures.PdfWithPhone();
        var extraction = RepositoryTextExtractor.Extract("Franco Ferrari - Resume.pdf", "application/pdf", pdf);
        Assert.Equal(RepositoryExtractionStatus.Ok, extraction.Status);
        Assert.Contains(WorkspaceDocumentFixtures.PhoneNumber, extraction.Text, StringComparison.Ordinal);
        Assert.Contains("Franco", extraction.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocxAndXlsxExtractionIncludePhoneNumber()
    {
        var docx = RepositoryTextExtractor.Extract("resume.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", WorkspaceDocumentFixtures.DocxWithPhone());
        Assert.Equal(RepositoryExtractionStatus.Ok, docx.Status);
        Assert.Contains(WorkspaceDocumentFixtures.PhoneNumber, docx.Text, StringComparison.Ordinal);

        var xlsx = RepositoryTextExtractor.Extract("contacts.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", WorkspaceDocumentFixtures.XlsxWithPhone());
        Assert.Equal(RepositoryExtractionStatus.Ok, xlsx.Status);
        Assert.Contains(WorkspaceDocumentFixtures.PhoneNumber, xlsx.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void DocxExtractionIncludesTableCellText()
    {
        var extraction = RepositoryTextExtractor.Extract(
            "scope.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            WorkspaceDocumentFixtures.DocxWithTableSection());
        Assert.Equal(RepositoryExtractionStatus.Ok, extraction.Status);
        Assert.Contains("Scope", extraction.Text, StringComparison.Ordinal);
        Assert.Contains("Table section: phase two budget", extraction.Text, StringComparison.Ordinal);
        Assert.Contains("Closing paragraph", extraction.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NamedFilePromptDoesNotDuplicateOverlappingChunks()
    {
        var overlap = " duplicated-bullet ";
        var context = new RepoDocumentContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "notes.docx",
            "Root / Hotel / notes.docx",
            ["alpha" + overlap, overlap + "omega"],
            ExtractedText: null,
            RepositoryExtractionStatus.Ok,
            null,
            NamedOrAttached: true);
        var rendered = WorkspaceChatComposer.RenderDocumentContents([context]);
        var first = rendered.IndexOf("duplicated-bullet", StringComparison.Ordinal);
        var last = rendered.LastIndexOf("duplicated-bullet", StringComparison.Ordinal);
        Assert.True(first >= 0);
        Assert.Equal(first, last);
        Assert.Contains("alpha", rendered, StringComparison.Ordinal);
        Assert.Contains("omega", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void NamesDocumentMatchesResumeTitleWithoutKeywordOverlap()
    {
        Assert.True(RepositoryNamedDocuments.NamesDocument(
            "What is the phone number in Franco Ferrari - Resume.pdf?",
            "Franco Ferrari - Resume.pdf"));
        Assert.True(RepositoryNamedDocuments.NamesDocument(
            "phone number for franco ferrari resume",
            "Franco Ferrari - Resume.pdf"));
        Assert.False(RepositoryNamedDocuments.NamesDocument("what is the phone number", "Franco Ferrari - Resume.pdf"));
    }

    [Fact]
    public async Task UploadPdfIndexesExtractedTextNotJustFilename()
    {
        await using var harness = await Harness.CreateAsync();
        await using var stream = new MemoryStream(WorkspaceDocumentFixtures.PdfWithPhone());
        var doc = await harness.Service.UploadAsync(
            harness.Actor,
            harness.RootId,
            "Franco Ferrari - Resume.pdf",
            "application/pdf",
            stream,
            CancellationToken.None);

        Assert.Equal(RepositoryExtractionStatus.Ok, doc.ExtractionStatus);
        Assert.Contains(WorkspaceDocumentFixtures.PhoneNumber, doc.PreviewText, StringComparison.Ordinal);
        var stored = await harness.Db.AgenticRepoDocuments.Include(d => d.Chunks).SingleAsync(d => d.Id == doc.Id);
        Assert.True(stored.IsIndexed);
        Assert.NotEmpty(stored.Chunks);
        Assert.Contains(stored.Chunks, c => c.Text.Contains(WorkspaceDocumentFixtures.PhoneNumber, StringComparison.Ordinal));
        Assert.DoesNotContain(stored.Chunks, c => RepositoryTextExtractor.IsMetadataOnly(c.Text, stored.Name));
    }

    [Fact]
    public async Task ChatContextIncludesNamedDocumentChunkWithPhoneNumber()
    {
        await using var harness = await Harness.CreateAsync();
        await using var stream = new MemoryStream(WorkspaceDocumentFixtures.PdfWithPhone());
        var doc = await harness.Service.UploadAsync(
            harness.Actor,
            harness.RootId,
            "Franco Ferrari - Resume.pdf",
            "application/pdf",
            stream,
            CancellationToken.None);

        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "The phone number is 555-0100.");
        var composer = new WorkspaceChatComposer(
            CreateRouter(hosted),
            new LocalRepositorySearch(harness.Db),
            NullLogger<WorkspaceChatComposer>.Instance);
        var chat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = harness.Actor.Oid,
            Title = "Resume questions",
        };

        var answer = await composer.ComposeAsync(
            chat,
            "What is the phone number in Franco Ferrari - Resume.pdf?",
            [],
            CancellationToken.None);

        Assert.Contains(WorkspaceDocumentFixtures.PhoneNumber, hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("Franco Ferrari - Resume.pdf", hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("phone numbers", hosted.LastSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("suggest uploading a file", hosted.LastSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(answer.Citations, c => c.DocumentId == doc.Id);
    }

    [Fact]
    public async Task AttachedDocumentIsIncludedEvenWhenKeywordsDoNotOverlap()
    {
        await using var harness = await Harness.CreateAsync();
        await using var stream = new MemoryStream(WorkspaceDocumentFixtures.PdfWithPhone());
        var doc = await harness.Service.UploadAsync(
            harness.Actor,
            harness.RootId,
            "Franco Ferrari - Resume.pdf",
            "application/pdf",
            stream,
            CancellationToken.None);
        var created = await harness.Service.CreateChatAsync(
            harness.Actor,
            new CreateChatRequestDto("About the resume", null, null, false, doc.Id),
            CancellationToken.None);
        Assert.Equal(doc.Id, created.DocumentId);
        Assert.Equal(doc.FolderId, created.FolderId);

        var storedChat = await harness.Db.AgenticChats.Include(c => c.Document).SingleAsync(c => c.Id == created.Id);
        var hosted = new ScriptedChatClient("openai", "gpt-4o-mini", "ok");
        var composer = new WorkspaceChatComposer(
            CreateRouter(hosted),
            new LocalRepositorySearch(harness.Db),
            NullLogger<WorkspaceChatComposer>.Instance);

        await composer.ComposeAsync(storedChat, "what is the phone number?", [], CancellationToken.None);

        Assert.Contains(WorkspaceDocumentFixtures.PhoneNumber, hosted.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("attached to the file", hosted.LastUserPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReindexRepairsEmptyMetadataOnlyChunks()
    {
        await using var harness = await Harness.CreateAsync();
        await using var stream = new MemoryStream(WorkspaceDocumentFixtures.PdfWithPhone());
        var doc = await harness.Service.UploadAsync(
            harness.Actor,
            harness.RootId,
            "Franco Ferrari - Resume.pdf",
            "application/pdf",
            stream,
            CancellationToken.None);

        var stored = await harness.Db.AgenticRepoDocuments.Include(d => d.Chunks).SingleAsync(d => d.Id == doc.Id);
        stored.ExtractedText = stored.Name;
        stored.ExtractionStatus = "";
        stored.IsIndexed = true;
        harness.Db.AgenticRepoChunks.RemoveRange(stored.Chunks);
        harness.Db.AgenticRepoChunks.Add(new AgenticRepoChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = stored.Id,
            ChunkIndex = 0,
            Text = stored.Name,
        });
        await harness.Db.SaveChangesAsync();

        var reindexed = await harness.Service.ReindexDocumentAsync(harness.Actor, stored.Id, CancellationToken.None);
        Assert.Equal(RepositoryExtractionStatus.Ok, reindexed.ExtractionStatus);
        Assert.Contains(WorkspaceDocumentFixtures.PhoneNumber, reindexed.PreviewText, StringComparison.Ordinal);

        var after = await harness.Db.AgenticRepoDocuments.Include(d => d.Chunks).SingleAsync(d => d.Id == stored.Id);
        Assert.Contains(after.Chunks, c => c.Text.Contains(WorkspaceDocumentFixtures.PhoneNumber, StringComparison.Ordinal));

        stored.ExtractedText = stored.Name;
        stored.ExtractionStatus = "";
        await harness.Db.SaveChangesAsync();
        var startup = await harness.Reindex.ReextractStaleDocumentsAsync(CancellationToken.None);
        Assert.True(startup >= 1);
        var repaired = await harness.Db.AgenticRepoDocuments.AsNoTracking().SingleAsync(d => d.Id == stored.Id);
        Assert.Equal(RepositoryExtractionStatus.Ok, repaired.ExtractionStatus);
        Assert.Contains(WorkspaceDocumentFixtures.PhoneNumber, repaired.ExtractedText, StringComparison.Ordinal);
    }

    private static ChatCompletionRouter CreateRouter(IChatCompletionClient hosted) =>
        new(
            new ScriptedChatClient("ollama", "llama3.1:8b", "local"),
            hosted,
            new ScriptedHealthProbe(false),
            new KnowledgeBaseFallbackOptions { ApiKey = "unit-test-placeholder-key" },
            NullLogger<ChatCompletionRouter>.Instance);

    private sealed class Harness : IAsyncDisposable
    {
        public required IntranetDbContext Db { get; init; }
        public required WorkspaceService Service { get; init; }
        public required IRepositoryReindexService Reindex { get; init; }
        public required Guid RootId { get; init; }
        public required string RootPath { get; init; }
        public WorkspaceActor Actor { get; } = new("tester", "Tester", "tester@etc.local");

        public static async Task<Harness> CreateAsync()
        {
            var db = new IntranetDbContext(new DbContextOptionsBuilder<IntranetDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            var rootPath = Path.Combine(Path.GetTempPath(), "ws-extract-" + Guid.NewGuid().ToString("N"));
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
            var reindex = new RepositoryReindexService(db, search, options, env, NullLogger<RepositoryReindexService>.Instance);
            var service = new WorkspaceService(
                db,
                new FakeDirectory(),
                search,
                reindex,
                new WorkspaceChatComposer(CreateRouter(hosted), search, NullLogger<WorkspaceChatComposer>.Instance),
                CreateRouter(hosted),
                options,
                env);
            return new Harness { Db = db, Service = service, Reindex = reindex, RootId = root.Id, RootPath = rootPath };
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
