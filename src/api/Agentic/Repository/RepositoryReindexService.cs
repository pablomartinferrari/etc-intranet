using Intranet.Api.Agentic.Data;
using Intranet.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Agentic.Repository;

public interface IRepositoryReindexService
{
    Task<int> ReextractStaleDocumentsAsync(CancellationToken cancellationToken);

    Task<int> ReextractDocumentAsync(AgenticRepoDocument document, CancellationToken cancellationToken);
}

public sealed class RepositoryReindexService : IRepositoryReindexService
{
    private readonly IntranetDbContext _db;
    private readonly IRepositorySearch _search;
    private readonly AgenticOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RepositoryReindexService> _logger;

    public RepositoryReindexService(
        IntranetDbContext db,
        IRepositorySearch search,
        IOptions<AgenticOptions> options,
        IWebHostEnvironment environment,
        ILogger<RepositoryReindexService> logger)
    {
        _db = db;
        _search = search;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<int> ReextractStaleDocumentsAsync(CancellationToken cancellationToken)
    {
        var documents = await _db.AgenticRepoDocuments
            .Include(d => d.Chunks)
            .Where(d =>
                d.ExtractionStatus == ""
                || d.ExtractionStatus == RepositoryExtractionStatus.Pending
                || ((d.ExtractedText == null || d.ExtractedText == "") && d.ExtractionStatus != RepositoryExtractionStatus.Empty
                    && d.ExtractionStatus != RepositoryExtractionStatus.Unsupported
                    && d.ExtractionStatus != RepositoryExtractionStatus.Error))
            .ToListAsync(cancellationToken);
        var count = 0;
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!RepositoryTextExtractor.NeedsStartupReextract(document.ExtractionStatus)
                && !RepositoryTextExtractor.IsMetadataOnly(document.ExtractedText, document.Name)
                && document.Chunks.Any(c => !RepositoryTextExtractor.IsMetadataOnly(c.Text, document.Name)))
            {
                document.ExtractionStatus = RepositoryExtractionStatus.Ok;
                continue;
            }

            count += await ReextractDocumentAsync(document, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        if (count > 0)
        {
            _logger.LogInformation("Re-extracted {Count} workspace repository documents.", count);
        }

        return count;
    }

    public async Task<int> ReextractDocumentAsync(AgenticRepoDocument document, CancellationToken cancellationToken)
    {
        var bytes = ReadStoredBytes(document);
        if (bytes is null)
        {
            document.ExtractedText = null;
            document.ExtractionStatus = RepositoryExtractionStatus.Error;
            document.ExtractionNote = "Stored file is missing; upload the file again or Re-index after restoring storage.";
            document.IsIndexed = false;
            await _search.RemoveDocumentAsync(document.Id, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return 1;
        }

        var extraction = RepositoryTextExtractor.Extract(document.Name, document.MimeType, bytes);
        document.ExtractedText = string.IsNullOrWhiteSpace(extraction.Text) ? null : extraction.Text;
        document.ExtractionStatus = extraction.Status;
        document.ExtractionNote = extraction.Note;
        await _search.ReindexDocumentAsync(document, cancellationToken);
        return 1;
    }

    private byte[]? ReadStoredBytes(AgenticRepoDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.StoragePath))
        {
            return null;
        }

        try
        {
            var root = RepositoryStorage.ResolveRoot(_options, _environment);
            var full = RepositoryStorage.CombineUnderRoot(root, document.StoragePath);
            return File.Exists(full) ? File.ReadAllBytes(full) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read stored workspace file {DocumentId}.", document.Id);
            return null;
        }
    }
}

public sealed class RepositoryReindexWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RepositoryReindexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            using var scope = scopeFactory.CreateScope();
            var reindex = scope.ServiceProvider.GetRequiredService<IRepositoryReindexService>();
            var count = await reindex.ReextractStaleDocumentsAsync(stoppingToken);
            logger.LogInformation("Workspace repository startup re-extract finished ({Count} documents).", count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workspace repository startup re-extract failed.");
        }
    }
}
