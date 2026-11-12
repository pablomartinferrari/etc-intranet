using Azure.Storage.Blobs;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.Services;

/// <summary>
/// Stores uploaded files where the ingest worker can read them (local disk or Azure Blob).
/// </summary>
public sealed class KnowledgeUploadStaging
{
    private readonly KnowledgeBaseOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<KnowledgeUploadStaging> _logger;

    public KnowledgeUploadStaging(
        IOptions<KnowledgeBaseOptions> options,
        IWebHostEnvironment env,
        ILogger<KnowledgeUploadStaging> logger)
    {
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public bool UsesAzureBlob =>
        !string.IsNullOrWhiteSpace(_options.AzureStorageConnectionString);

    public async Task<StagedUpload> StageAsync(
        Guid documentId,
        string safeName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (UsesAzureBlob)
        {
            return await StageToBlobAsync(documentId, safeName, content, cancellationToken);
        }

        return await StageToLocalAsync(documentId, safeName, content, cancellationToken);
    }

    private async Task<StagedUpload> StageToBlobAsync(
        Guid documentId,
        string safeName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var container = _options.AzureStorageContainer;
        var blobName = $"{documentId}/{safeName}";
        var storageUri = $"azure://{container}/{blobName}";

        var client = new BlobContainerClient(_options.AzureStorageConnectionString!, container);
        await client.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = client.GetBlobClient(blobName);
        await blob.UploadAsync(content, overwrite: true, cancellationToken);

        _logger.LogInformation(
            "Staged upload {DocumentId} to blob {BlobName}",
            documentId,
            blobName);

        return new StagedUpload(
            storageUri,
            new Dictionary<string, string?>
            {
                ["document_id"] = documentId.ToString(),
                ["storage_uri"] = storageUri,
                ["name"] = safeName,
            });
    }

    private async Task<StagedUpload> StageToLocalAsync(
        Guid documentId,
        string safeName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var stagingRoot = ResolvePath(_options.IngestStagingPath);
        var docDir = Path.Combine(stagingRoot, documentId.ToString());
        Directory.CreateDirectory(docDir);
        var stagedPath = Path.Combine(docDir, safeName);

        await using (var file = File.Create(stagedPath))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        _logger.LogInformation(
            "Staged upload {DocumentId} to local path {Path}",
            documentId,
            stagedPath);

        return new StagedUpload(
            stagedPath,
            new Dictionary<string, string?>
            {
                ["document_id"] = documentId.ToString(),
                ["path"] = stagedPath,
                ["name"] = safeName,
            });
    }

    private string ResolvePath(string path) =>
        Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, path));
}

public sealed record StagedUpload(string StorageUri, IReadOnlyDictionary<string, string?> JobPayload);
