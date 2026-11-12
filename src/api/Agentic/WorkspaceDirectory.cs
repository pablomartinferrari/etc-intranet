using Intranet.Api.KnowledgeBase.Services;

namespace Intranet.Api.Agentic;

/// <summary>
/// Directory search for Share. Reuses the existing Graph client when configured
/// and always includes the local demo principals so the POC share dialog works
/// without Entra admin consent.
/// </summary>
public interface IWorkspaceDirectory
{
    IReadOnlyList<DirectoryPrincipalDto> LocalPrincipals { get; }

    Task<IReadOnlyList<DirectoryPrincipalDto>> SearchAsync(string query, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
        string userOid,
        IReadOnlyCollection<string> groupOids,
        CancellationToken cancellationToken);
}

public sealed class WorkspaceDirectory : IWorkspaceDirectory
{
    private readonly IGraphDirectoryClient _graph;

    public WorkspaceDirectory(IGraphDirectoryClient graph)
    {
        _graph = graph;
    }

    public IReadOnlyList<DirectoryPrincipalDto> LocalPrincipals { get; } =
    [
        new(AgenticRoles.User, WorkspaceIds.DemoYouOid, "You", "you@etc.local", null),
        new(AgenticRoles.User, WorkspaceIds.DemoUserBOid, "User B", "user.b@etc.local", null),
        new(AgenticRoles.User, WorkspaceIds.DemoUserCOid, "User C", "user.c@etc.local", null),
        new(AgenticRoles.User, WorkspaceIds.DemoUserDOid, "User D", "user.d@etc.local", null),
        new(AgenticRoles.Group, WorkspaceIds.DemoGroupXOid, "Group X", null, 12),
    ];

    public async Task<IReadOnlyList<DirectoryPrincipalDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var q = query.Trim();
        var local = LocalPrincipals
            .Where(p =>
                p.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (p.Email?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || p.Oid.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!_graph.IsConfigured || q.Length == 0)
        {
            return local;
        }

        try
        {
            var remote = await _graph.SearchAsync(q, cancellationToken);
            var seen = new HashSet<string>(local.Select(p => p.Oid), StringComparer.OrdinalIgnoreCase);
            foreach (var item in remote)
            {
                if (!seen.Add(item.Oid))
                {
                    continue;
                }

                local.Add(new DirectoryPrincipalDto(item.Type, item.Oid, item.DisplayName, item.Email, null));
            }
        }
        catch (GraphDirectoryException)
        {
            // Local demo principals still work.
        }

        return local;
    }

    public async Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
        string userOid,
        IReadOnlyCollection<string> groupOids,
        CancellationToken cancellationToken)
    {
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AgenticAccess.OidsEqual(userOid, WorkspaceIds.DemoYouOid)
            && groupOids.Any(g => AgenticAccess.OidsEqual(g, WorkspaceIds.DemoGroupXOid)))
        {
            matches.Add(WorkspaceIds.DemoGroupXOid);
        }

        if (_graph.IsConfigured && !userOid.StartsWith("dev-", StringComparison.OrdinalIgnoreCase))
        {
            var remote = await _graph.CheckMemberGroupsAsync(userOid, groupOids, cancellationToken);
            foreach (var oid in remote)
            {
                matches.Add(oid);
            }
        }

        return matches;
    }
}
