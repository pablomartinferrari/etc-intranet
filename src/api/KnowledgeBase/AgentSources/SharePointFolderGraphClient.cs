using Azure.Identity;
using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.MultifamilyLbp.Options;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Intranet.Api.KnowledgeBase.AgentSources;

public sealed class SharePointFolderGraphClient : ISharePointFolderGraph
{
    private readonly AzureAdOptions _azureAd;
    private readonly AgentSourceOptions _limits;
    private readonly ILogger<SharePointFolderGraphClient> _logger;

    public SharePointFolderGraphClient(
        IOptions<AzureAdOptions> azureAd,
        IOptions<KnowledgeBaseOptions> knowledge,
        ILogger<SharePointFolderGraphClient> logger)
    {
        _azureAd = azureAd.Value;
        _limits = knowledge.Value.AgentSources;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_azureAd.TenantId)
        && !string.Equals(_azureAd.TenantId, "common", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_azureAd.ClientId)
        && !_azureAd.ClientId.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_azureAd.ClientSecret);

    public async Task<SharePointProbeResult> ProbeAsync(
        SharePointFolderRef folder,
        CancellationToken cancellationToken)
    {
        var stats = new CrawlStats();
        await foreach (var _ in CrawlAsync(folder, collectFiles: false, stats, cancellationToken))
        {
        }

        return stats.ToProbeResult();
    }

    public async IAsyncEnumerable<SharePointDriveFile> EnumerateAllowedFilesAsync(
        SharePointFolderRef folder,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stats = new CrawlStats();
        await foreach (var file in CrawlAsync(folder, collectFiles: true, stats, cancellationToken))
        {
            yield return file;
        }
    }

    public async Task<byte[]> DownloadAsync(string driveId, string itemId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        try
        {
            var client = CreateClient();
            await using var stream = await client.Drives[driveId].Items[itemId].Content
                .GetAsync(cancellationToken: cancellationToken)
                ?? throw new AgentSourceException("SharePoint returned an empty file.", 502);
            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            return ms.ToArray();
        }
        catch (Exception ex) when (ex is not AgentSourceException and not OperationCanceledException)
        {
            throw MapGraphError(ex, "Could not download the SharePoint file.");
        }
    }

    private async IAsyncEnumerable<SharePointDriveFile> CrawlAsync(
        SharePointFolderRef folder,
        bool collectFiles,
        CrawlStats stats,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, _limits.ProbeTimeoutSeconds)));
        var ct = timeoutCts.Token;

        GraphServiceClient client;
        DriveItem root;
        string driveId;
        try
        {
            client = CreateClient();
            (root, driveId) = await ResolveFolderAsync(client, folder, ct);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stats.Truncated = true;
            yield break;
        }
        catch (Exception ex) when (ex is not AgentSourceException)
        {
            throw MapGraphError(ex, "Could not open that SharePoint folder.");
        }

        stats.DriveId = driveId;
        stats.ItemId = root.Id;
        stats.FolderName = root.Name;
        stats.WebUrl = root.WebUrl;

        var queue = new Queue<(string ItemId, int Depth)>();
        queue.Enqueue((root.Id ?? "", 0));
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            if (stats.ItemsVisited >= _limits.ProbeMaxItems)
            {
                stats.Truncated = true;
                yield break;
            }

            var (itemId, depth) = queue.Dequeue();
            if (string.IsNullOrEmpty(itemId) || !visited.Add(itemId))
            {
                continue;
            }

            stats.MaxDepthReached = Math.Max(stats.MaxDepthReached, depth);
            IReadOnlyList<DriveItem> children;
            try
            {
                children = await ListChildrenAsync(client, driveId, itemId, ct);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stats.Truncated = true;
                yield break;
            }
            catch (Exception ex) when (ex is not AgentSourceException)
            {
                throw MapGraphError(ex, "Could not list files in that SharePoint folder.");
            }

            foreach (var child in children)
            {
                stats.ItemsVisited++;
                if (child.Folder is not null)
                {
                    if (depth + 1 <= _limits.MaxDepth && !string.IsNullOrEmpty(child.Id))
                    {
                        queue.Enqueue((child.Id, depth + 1));
                    }

                    continue;
                }

                if (child.File is null)
                {
                    continue;
                }

                var name = child.Name ?? "file";
                var size = child.Size ?? 0;
                stats.FileCount++;
                stats.TotalBytes += size;
                stats.NoteExtension(AgentSourceFileRules.GetExtension(name));

                if (!AgentSourceFileRules.ShouldIngest(name, size, _limits))
                {
                    stats.SkippedFiles++;
                    continue;
                }

                stats.AllowedFiles++;
                stats.AllowedBytes += size;
                if (collectFiles && !string.IsNullOrEmpty(child.Id))
                {
                    yield return new SharePointDriveFile(
                        driveId,
                        child.Id,
                        name,
                        size,
                        child.WebUrl,
                        depth);
                }
            }
        }
    }

    private async Task<(DriveItem Folder, string DriveId)> ResolveFolderAsync(
        GraphServiceClient client,
        SharePointFolderRef folder,
        CancellationToken cancellationToken)
    {
        Site? site;
        try
        {
            site = await client.Sites[folder.SiteKey].GetAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            throw MapGraphError(ex, $"Could not resolve SharePoint site {folder.SiteUrl}.");
        }

        if (string.IsNullOrEmpty(site?.Id))
        {
            throw new AgentSourceException("SharePoint site has no id. Check the site URL.", 404);
        }

        Drive? drive;
        try
        {
            drive = await client.Sites[site.Id].Drive.GetAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            throw MapGraphError(ex, "Could not open the default document library for that site.");
        }

        if (string.IsNullOrEmpty(drive?.Id))
        {
            throw new AgentSourceException("SharePoint site has no default document library.", 404);
        }

        var pathCandidates = FolderPathCandidates(folder.FolderPath);
        Exception? last = null;
        foreach (var path in pathCandidates)
        {
            try
            {
                DriveItem? item;
                if (string.IsNullOrEmpty(path))
                {
                    item = await client.Drives[drive.Id].Root.GetAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    item = await client.Drives[drive.Id].Root
                        .ItemWithPath(path)
                        .GetAsync(cancellationToken: cancellationToken);
                }

                if (item?.Id is not null)
                {
                    return (item, drive.Id);
                }
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw MapGraphError(
            last ?? new InvalidOperationException("Folder not found."),
            $"Folder '{folder.FolderPath}' was not found on that site. Check the path (for example Shared Documents/Policies).");
    }

    private static IReadOnlyList<string> FolderPathCandidates(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return [""];
        }

        var path = folderPath.Trim().Trim('/');
        var list = new List<string> { path };
        foreach (var prefix in new[] { "Shared Documents/", "Documents/", "Shared Documents", "Documents" })
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = path[prefix.Length..].Trim('/');
                if (!list.Contains(stripped, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(stripped);
                }
            }
        }

        return list;
    }

    private static async Task<IReadOnlyList<DriveItem>> ListChildrenAsync(
        GraphServiceClient client,
        string driveId,
        string itemId,
        CancellationToken cancellationToken)
    {
        var items = new List<DriveItem>();
        var page = await client.Drives[driveId].Items[itemId].Children.GetAsync(config =>
        {
            config.QueryParameters.Select = ["id", "name", "size", "file", "folder", "webUrl"];
            config.QueryParameters.Top = 200;
        }, cancellationToken);

        while (page is not null)
        {
            if (page.Value is { Count: > 0 })
            {
                items.AddRange(page.Value);
            }

            if (string.IsNullOrEmpty(page.OdataNextLink))
            {
                break;
            }

            page = await client.Drives[driveId].Items[itemId].Children
                .WithUrl(page.OdataNextLink)
                .GetAsync(cancellationToken: cancellationToken);
        }

        return items;
    }

    private GraphServiceClient CreateClient()
    {
        var credential = new ClientSecretCredential(_azureAd.TenantId, _azureAd.ClientId, _azureAd.ClientSecret);
        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }

    private void EnsureConfigured()
    {
        if (IsConfigured)
        {
            return;
        }

        throw new AgentSourceException(
            "SharePoint Graph is not configured. Set AzureAd__TenantId, AzureAd__ClientId, and AzureAd__ClientSecret, and grant the Entra app Sites.Read.All and Files.Read.All (application) with admin consent.",
            503);
    }

    private AgentSourceException MapGraphError(Exception ex, string fallback)
    {
        _logger.LogWarning(ex, "SharePoint Graph call failed: {Message}", fallback);
        if (ex is ODataError odata)
        {
            var code = odata.Error?.Code ?? "";
            var status = odata.ResponseStatusCode;
            if (status == 403
                || code.Contains("accessDenied", StringComparison.OrdinalIgnoreCase)
                || code.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
            {
                return new AgentSourceException(
                    "The intranet app cannot read this SharePoint folder. An admin needs to grant Graph Sites.Read.All and Files.Read.All (application permission) on the Entra app, consent, and allow the app on this site.",
                    403,
                    ex);
            }

            if (status == 401 || code.Contains("unauthenticated", StringComparison.OrdinalIgnoreCase))
            {
                return new AgentSourceException(
                    "SharePoint Graph authentication failed. Check AzureAd ClientId, TenantId, and ClientSecret.",
                    401,
                    ex);
            }

            if (status == 404 || code.Contains("itemNotFound", StringComparison.OrdinalIgnoreCase))
            {
                return new AgentSourceException(
                    "SharePoint site or folder was not found. Check the site URL and folder path.",
                    404,
                    ex);
            }

            var graphMessage = odata.Error?.Message;
            if (!string.IsNullOrWhiteSpace(graphMessage))
            {
                return new AgentSourceException($"{fallback} {graphMessage}", status is >= 400 and < 600 ? status : 502, ex);
            }
        }

        return new AgentSourceException($"{fallback} {ex.Message}", 502, ex);
    }

    private sealed class CrawlStats
    {
        public int ItemsVisited;
        public int FileCount;
        public long TotalBytes;
        public int AllowedFiles;
        public long AllowedBytes;
        public int SkippedFiles;
        public int MaxDepthReached;
        public bool Truncated;
        public string? DriveId;
        public string? ItemId;
        public string? FolderName;
        public string? WebUrl;
        private readonly Dictionary<string, int> _extensions = new(StringComparer.OrdinalIgnoreCase);

        public void NoteExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext))
            {
                ext = "(none)";
            }

            _extensions[ext] = _extensions.GetValueOrDefault(ext) + 1;
        }

        public SharePointProbeResult ToProbeResult() => new()
        {
            FileCount = FileCount,
            TotalBytes = TotalBytes,
            AllowedFiles = AllowedFiles,
            AllowedBytes = AllowedBytes,
            SkippedFiles = SkippedFiles,
            MaxDepthReached = MaxDepthReached,
            SampleExtensions = _extensions
                .OrderByDescending(kv => kv.Value)
                .Take(12)
                .Select(kv => kv.Key)
                .ToList(),
            Truncated = Truncated,
            DriveId = DriveId,
            ItemId = ItemId,
            FolderName = FolderName,
            WebUrl = WebUrl,
        };
    }
}
