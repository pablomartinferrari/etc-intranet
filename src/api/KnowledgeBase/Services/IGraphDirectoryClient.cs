using Intranet.Api.KnowledgeBase.Models;

namespace Intranet.Api.KnowledgeBase.Services;

public interface IGraphDirectoryClient
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<DirectoryPrincipalDto>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the subset of <paramref name="groupOids"/> that <paramref name="userOid"/> is a member of.
    /// Empty when Graph is not configured or the call fails.
    /// </summary>
    Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
        string userOid,
        IReadOnlyCollection<string> groupOids,
        CancellationToken cancellationToken);
}

public sealed class GraphDirectoryException : Exception
{
    public int StatusCode { get; }

    public GraphDirectoryException(string message, int statusCode = StatusCodes.Status503ServiceUnavailable)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
