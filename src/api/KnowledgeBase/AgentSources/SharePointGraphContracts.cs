namespace Intranet.Api.KnowledgeBase.AgentSources;

public class AgentSourceException : Exception
{
    public AgentSourceException(string message, int statusCode = 400, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed record SharePointDriveFile(
    string DriveId,
    string ItemId,
    string Name,
    long Size,
    string? WebUrl,
    int Depth);

public sealed class SharePointProbeResult
{
    public int FileCount { get; init; }
    public long TotalBytes { get; init; }
    public int AllowedFiles { get; init; }
    public long AllowedBytes { get; init; }
    public int SkippedFiles { get; init; }
    public int MaxDepthReached { get; init; }
    public IReadOnlyList<string> SampleExtensions { get; init; } = [];
    public bool Truncated { get; init; }
    public string? DriveId { get; init; }
    public string? ItemId { get; init; }
    public string? FolderName { get; init; }
    public string? WebUrl { get; init; }
}

public interface ISharePointFolderGraph
{
    bool IsConfigured { get; }

    Task<SharePointProbeResult> ProbeAsync(
        SharePointFolderRef folder,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SharePointDriveFile> EnumerateAllowedFilesAsync(
        SharePointFolderRef folder,
        CancellationToken cancellationToken);

    Task<byte[]> DownloadAsync(string driveId, string itemId, CancellationToken cancellationToken);
}
