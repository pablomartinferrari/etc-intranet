using Intranet.Api.Data;
using Intranet.Api.FeatureRequests;
using Intranet.Api.KnowledgeBase.AgentSources;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class AgentSourceServiceTests
{
    [Fact]
    public async Task ProbeReturnsSoftDecision()
    {
        await using var db = CreateDb();
        var graph = new FakeGraph
        {
            Probe = new SharePointProbeResult
            {
                FileCount = 4,
                TotalBytes = 4000,
                AllowedFiles = 3,
                AllowedBytes = 3000,
                SkippedFiles = 1,
                MaxDepthReached = 2,
                SampleExtensions = [".pdf", ".mp4"],
            },
        };
        var service = CreateService(db, graph);

        var probe = await service.ProbeAsync(
            "https://contoso.sharepoint.com/sites/HR",
            "Shared Documents/Policies",
            CancellationToken.None);

        Assert.Equal("soft", probe.LimitTier);
        Assert.True(probe.CanAutoRun);
        Assert.Equal(3, probe.AllowedFiles);
        Assert.Contains(".pdf", probe.SampleExtensions);
    }

    [Fact]
    public async Task ProbeRejectsMissingUrl()
    {
        await using var db = CreateDb();
        var service = CreateService(db, new FakeGraph());
        var error = await Assert.ThrowsAsync<AgentSourceException>(() =>
            service.ProbeAsync(" ", null, CancellationToken.None));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task ConnectSoftEnqueuesQueuedJob()
    {
        await using var db = CreateDb();
        var graph = SmallFolderGraph();
        var service = CreateService(db, graph);

        var source = await service.ConnectAsync(
            new Intranet.Api.KnowledgeBase.Models.AgentSourceConnectRequestDto(
                "https://contoso.sharepoint.com/sites/HR",
                "Docs",
                "HR docs",
                false),
            "oid-1",
            "pablo@etc.example",
            CancellationToken.None);

        Assert.Equal("connected", source.Status);
        Assert.Equal("queued", source.LatestJob!.Status);
        Assert.Equal("soft", source.LatestJob.LimitTier);
        Assert.Equal("HR docs", source.Label);
        Assert.Single(db.AgentSources);
        Assert.Single(db.AgentSourceJobs);
        Assert.Empty(db.FeatureRequests);
    }

    [Fact]
    public async Task ConnectMediumWithoutConfirmThrows()
    {
        await using var db = CreateDb();
        var graph = new FakeGraph
        {
            Probe = new SharePointProbeResult
            {
                FileCount = 3_500,
                AllowedFiles = 3_500,
                AllowedBytes = 100,
                SampleExtensions = [".pdf"],
            },
        };
        var service = CreateService(db, graph);

        var error = await Assert.ThrowsAsync<AgentSourceConfirmRequiredException>(() =>
            service.ConnectAsync(
                new Intranet.Api.KnowledgeBase.Models.AgentSourceConnectRequestDto(
                    "https://contoso.sharepoint.com/sites/HR",
                    "Big",
                    null,
                    ConfirmMedium: false),
                "oid-1",
                "pablo@etc.example",
                CancellationToken.None));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("medium", error.Probe.LimitTier);
        Assert.Empty(db.AgentSources);
    }

    [Fact]
    public async Task ConnectMediumWithConfirmEnqueues()
    {
        await using var db = CreateDb();
        var graph = new FakeGraph
        {
            Probe = new SharePointProbeResult
            {
                FileCount = 3_500,
                AllowedFiles = 3_500,
                AllowedBytes = 100,
                SampleExtensions = [".pdf"],
            },
        };
        var service = CreateService(db, graph);

        var source = await service.ConnectAsync(
            new Intranet.Api.KnowledgeBase.Models.AgentSourceConnectRequestDto(
                "https://contoso.sharepoint.com/sites/HR",
                "Big",
                null,
                ConfirmMedium: true),
            "oid-1",
            "pablo@etc.example",
            CancellationToken.None);

        Assert.Equal("queued", source.LatestJob!.Status);
        Assert.Equal("medium", source.LatestJob.LimitTier);
        Assert.Empty(db.FeatureRequests);
    }

    [Fact]
    public async Task ConnectHardCreatesApprovalRequest()
    {
        await using var db = CreateDb();
        var graph = new FakeGraph
        {
            Probe = new SharePointProbeResult
            {
                FileCount = 25_000,
                AllowedFiles = 25_000,
                AllowedBytes = 50L * 1024 * 1024 * 1024,
                SampleExtensions = [".pdf"],
            },
        };
        var service = CreateService(db, graph);

        var source = await service.ConnectAsync(
            new Intranet.Api.KnowledgeBase.Models.AgentSourceConnectRequestDto(
                "https://contoso.sharepoint.com/sites/Legal",
                "Archive",
                "Legal archive",
                false),
            "oid-1",
            "pablo@etc.example",
            CancellationToken.None);

        Assert.Equal("awaiting_approval", source.Status);
        Assert.Equal("awaiting_approval", source.LatestJob!.Status);
        Assert.Equal("hard", source.LatestJob.LimitTier);
        Assert.NotNull(source.ApprovalRequestId);
        var ticket = Assert.Single(db.FeatureRequests);
        Assert.Equal("other", ticket.Page);
        Assert.Equal("Chat agent sources", ticket.AreaLabel);
        Assert.Contains("ingestible files", ticket.RawText, StringComparison.Ordinal);
        Assert.Contains("sites/Legal", ticket.RawText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectRejectsDuplicateFolder()
    {
        await using var db = CreateDb();
        var service = CreateService(db, SmallFolderGraph());
        var request = new Intranet.Api.KnowledgeBase.Models.AgentSourceConnectRequestDto(
            "https://contoso.sharepoint.com/sites/HR",
            "Docs",
            null,
            false);
        await service.ConnectAsync(request, "oid-1", "a@etc.example", CancellationToken.None);

        var error = await Assert.ThrowsAsync<AgentSourceException>(() =>
            service.ConnectAsync(request, "oid-2", "b@etc.example", CancellationToken.None));
        Assert.Equal(409, error.StatusCode);
        Assert.Contains("already connected", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(db.AgentSources);
    }

    [Fact]
    public async Task ProcessNextRunsQueuedToDoneAndIndexesFiles()
    {
        await using var db = CreateDb();
        var graph = SmallFolderGraph();
        graph.Files =
        [
            new SharePointDriveFile("drive", "item-1", "handbook.txt", 12, "https://sp/handbook.txt", 1),
        ];
        graph.Downloads["item-1"] = "Safety handbook contents for Chat."u8.ToArray();
        var upsert = new RecordingUpsert();
        var service = CreateService(db, graph, upsert);

        await service.ConnectAsync(
            new Intranet.Api.KnowledgeBase.Models.AgentSourceConnectRequestDto(
                "https://contoso.sharepoint.com/sites/HR",
                "Docs",
                null,
                false),
            "oid-1",
            "pablo@etc.example",
            CancellationToken.None);

        Assert.True(await service.ProcessNextAsync(CancellationToken.None));
        var job = Assert.Single(db.AgentSourceJobs);
        Assert.Equal("done", job.Status);
        Assert.Equal(1, job.FilesProcessed);
        Assert.NotNull(job.StartedAt);
        Assert.NotNull(job.FinishedAt);
        Assert.Equal("handbook.txt", Assert.Single(upsert.Titles));
        Assert.False(await service.ProcessNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProcessNextRecordsGraphFailure()
    {
        await using var db = CreateDb();
        var graph = SmallFolderGraph();
        graph.EnumerateError = new AgentSourceException(
            "The intranet app cannot read this SharePoint folder.",
            403);
        var service = CreateService(db, graph);

        await service.ConnectAsync(
            new Intranet.Api.KnowledgeBase.Models.AgentSourceConnectRequestDto(
                "https://contoso.sharepoint.com/sites/HR",
                "Docs",
                null,
                false),
            "oid-1",
            "pablo@etc.example",
            CancellationToken.None);

        Assert.True(await service.ProcessNextAsync(CancellationToken.None));
        var job = Assert.Single(db.AgentSourceJobs);
        Assert.Equal("failed", job.Status);
        Assert.Contains("cannot read", job.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisconnectStopsQueuedJobAndMarksDocsInactive()
    {
        await using var db = CreateDb();
        var upsert = new RecordingUpsert();
        var service = CreateService(db, SmallFolderGraph(), upsert);
        var source = await service.ConnectAsync(
            new Intranet.Api.KnowledgeBase.Models.AgentSourceConnectRequestDto(
                "https://contoso.sharepoint.com/sites/HR",
                "Docs",
                null,
                false),
            "oid-1",
            "pablo@etc.example",
            CancellationToken.None);

        db.AgentSourceDocuments.Add(new()
        {
            SourceId = source.Id,
            DocumentId = Guid.NewGuid(),
            AddedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        await service.DisconnectAsync(source.Id, CancellationToken.None);
        var stored = Assert.Single(db.AgentSources);
        Assert.Equal("disconnected", stored.Status);
        Assert.Equal("failed", stored.Jobs.Single().Status);
        Assert.Single(upsert.InactiveIds);
    }

    private static AgentSourceService CreateService(
        IntranetDbContext db,
        FakeGraph graph,
        RecordingUpsert? upsert = null)
    {
        var features = new FeatureRequestService(db, new NullLlm());
        return new AgentSourceService(
            db,
            graph,
            new FakeEmbeddings(),
            upsert ?? new RecordingUpsert(),
            features,
            Options.Create(new KnowledgeBaseOptions()),
            NullLogger<AgentSourceService>.Instance);
    }

    private static FakeGraph SmallFolderGraph() => new()
    {
        Probe = new SharePointProbeResult
        {
            FileCount = 2,
            TotalBytes = 200,
            AllowedFiles = 2,
            AllowedBytes = 200,
            SampleExtensions = [".txt", ".pdf"],
        },
    };

    private static IntranetDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IntranetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IntranetDbContext(options);
    }

    private sealed class NullLlm : IFeatureRequestLlm
    {
        public Task<string?> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakeGraph : ISharePointFolderGraph
    {
        public bool IsConfigured { get; set; } = true;
        public SharePointProbeResult Probe { get; set; } = new();
        public List<SharePointDriveFile> Files { get; set; } = [];
        public Dictionary<string, byte[]> Downloads { get; } = new(StringComparer.Ordinal);
        public Exception? EnumerateError { get; set; }

        public Task<SharePointProbeResult> ProbeAsync(SharePointFolderRef folder, CancellationToken cancellationToken) =>
            Task.FromResult(Probe);

        public async IAsyncEnumerable<SharePointDriveFile> EnumerateAllowedFilesAsync(
            SharePointFolderRef folder,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (EnumerateError is not null)
            {
                throw EnumerateError;
            }

            foreach (var file in Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
                await Task.Yield();
            }
        }

        public Task<byte[]> DownloadAsync(string driveId, string itemId, CancellationToken cancellationToken) =>
            Task.FromResult(Downloads.GetValueOrDefault(itemId) ?? "document"u8.ToArray());
    }

    private sealed class FakeEmbeddings : IHostedEmbeddingClient
    {
        public bool IsConfigured => true;
        public string ModelName => "test-embed";

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new[] { 0.1f, 0.2f }).ToList());
    }

    private sealed class RecordingUpsert : IKnowledgeDocumentUpsert
    {
        public List<string> Titles { get; } = [];
        public List<Guid> InactiveIds { get; } = [];

        public Task<Guid> UpsertSharePointDocumentAsync(
            Guid sourceJobId,
            string title,
            string? sourceUri,
            string? externalId,
            string? mimeType,
            string? uploadedByOid,
            IReadOnlyList<string> chunks,
            IReadOnlyList<float[]>? embeddings,
            string embeddingModel,
            CancellationToken cancellationToken)
        {
            Titles.Add(title);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task MarkDocumentsInactiveAsync(IReadOnlyList<Guid> documentIds, CancellationToken cancellationToken)
        {
            InactiveIds.AddRange(documentIds);
            return Task.CompletedTask;
        }
    }
}
