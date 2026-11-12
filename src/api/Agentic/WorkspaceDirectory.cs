using Intranet.Api.Agentic.Day;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace Intranet.Api.Agentic;

/// <summary>
/// Directory search for Share. Production uses Graph only (no demo principals).
/// Prefers delegated OBO (<c>User.ReadBasic.All</c> / <c>Group.Read.All</c>) with
/// <c>$search</c>, then app-only <see cref="IGraphDirectoryClient"/> (User.Read.All /
/// Group.Read.All). Development can include local demo principals when
/// ShareDemoCatalog/AllowAnonymous is on.
/// </summary>
public interface IWorkspaceDirectory
{
    IReadOnlyList<DirectoryPrincipalDto> LocalPrincipals { get; }

    bool GraphConfigured { get; }

    Task<IReadOnlyList<DirectoryPrincipalDto>> SearchAsync(string query, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
        string userOid,
        IReadOnlyCollection<string> groupOids,
        CancellationToken cancellationToken);
}

public sealed class WorkspaceDirectory : IWorkspaceDirectory
{
    public static readonly IReadOnlyList<DirectoryPrincipalDto> DemoPrincipals =
    [
        new(AgenticRoles.User, WorkspaceIds.DemoYouOid, "You", "you@etc.local", null),
        new(AgenticRoles.User, WorkspaceIds.DemoUserBOid, "User B", "user.b@etc.local", null),
        new(AgenticRoles.User, WorkspaceIds.DemoUserCOid, "User C", "user.c@etc.local", null),
        new(AgenticRoles.User, WorkspaceIds.DemoUserDOid, "User D", "user.d@etc.local", null),
        new(AgenticRoles.Group, WorkspaceIds.DemoGroupXOid, "Group X", null, 12),
    ];

    private readonly IGraphDirectoryClient _graph;
    private readonly IUserGraphTokenProvider _tokens;
    private readonly AgenticOptions _options;
    private readonly ILogger<WorkspaceDirectory> _logger;

    public WorkspaceDirectory(
        IGraphDirectoryClient graph,
        IUserGraphTokenProvider tokens,
        IOptions<AgenticOptions> options,
        ILogger<WorkspaceDirectory> logger)
    {
        _graph = graph;
        _tokens = tokens;
        _options = options.Value;
        _logger = logger;
    }

    public bool GraphConfigured => _graph.IsConfigured || _tokens.IsConfigured;

    public IReadOnlyList<DirectoryPrincipalDto> LocalPrincipals =>
        _options.IncludeDemoDirectory ? DemoPrincipals : [];

    public async Task<IReadOnlyList<DirectoryPrincipalDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var q = query.Trim();
        var local = LocalPrincipals
            .Where(p =>
                q.Length > 0
                && (p.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (p.Email?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || p.Oid.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var looksLikeEmail = GraphPeopleQueries.LooksLikeEmail(q);
        if (q.Length == 0)
        {
            return local;
        }

        if (q.Length < GraphPeopleQueries.MinQueryLength && !looksLikeEmail)
        {
            return local;
        }

        var seen = new HashSet<string>(local.Select(p => p.Oid), StringComparer.OrdinalIgnoreCase);
        var merged = new List<DirectoryPrincipalDto>(local);
        var errors = new List<Exception>();

        if (_tokens.IsConfigured)
        {
            await TryDelegatedSearchAsync(q, looksLikeEmail, seen, merged, errors, cancellationToken);
        }

        if (merged.Count == local.Count && _graph.IsConfigured)
        {
            try
            {
                var remote = await _graph.SearchAsync(q, cancellationToken);
                AddRange(remote, seen, merged);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "App-only Graph directory search failed. Code={Code}.", GraphPeopleQueries.ErrorCode(ex) ?? "(none)");
                errors.Add(ex);
            }
        }

        if (merged.Count > local.Count)
        {
            return merged;
        }

        if (errors.Count > 0)
        {
            throw ToDirectoryException(errors[0]);
        }

        if (!_tokens.IsConfigured && !_graph.IsConfigured && local.Count == 0)
        {
            throw new GraphDirectoryException(
                "Microsoft Graph is not configured for directory search. " +
                "Set AzureAd__TenantId, AzureAd__ClientId, and AzureAd__ClientSecret, then grant " +
                "delegated User.ReadBasic.All and Group.Read.All (or application User.Read.All and Group.Read.All) " +
                "with admin consent on the intranet web API app registration.",
                StatusCodes.Status503ServiceUnavailable);
        }

        return merged;
    }

    public async Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
        string userOid,
        IReadOnlyCollection<string> groupOids,
        CancellationToken cancellationToken)
    {
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_options.IncludeDemoDirectory
            && AgenticAccess.OidsEqual(userOid, WorkspaceIds.DemoYouOid)
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

    private async Task TryDelegatedSearchAsync(
        string query,
        bool looksLikeEmail,
        HashSet<string> seen,
        List<DirectoryPrincipalDto> merged,
        List<Exception> errors,
        CancellationToken cancellationToken)
    {
        var usersTask = SearchWithTokenAsync(
            "users",
            () => _tokens.AcquireDirectoryUserTokenAsync(cancellationToken),
            (search, ct) => search.SearchUsersAsync(query, ct),
            cancellationToken);
        var groupsTask = SearchWithTokenAsync(
            "groups",
            () => _tokens.AcquireDirectoryGroupTokenAsync(cancellationToken),
            (search, ct) => search.SearchGroupsAsync(query, ct),
            cancellationToken);
        await Task.WhenAll(usersTask, groupsTask);

        AddAttempt(usersTask.Result, seen, merged, errors);
        AddAttempt(groupsTask.Result, seen, merged, errors);

        if (looksLikeEmail && merged.All(p => !string.Equals(p.Email, query, StringComparison.OrdinalIgnoreCase)))
        {
            var resolved = await SearchWithTokenAsync(
                "users-resolve",
                () => _tokens.AcquireDirectoryUserTokenAsync(cancellationToken),
                (search, ct) => search.ResolveUserAsync(query, ct),
                cancellationToken);
            AddAttempt(resolved, seen, merged, errors);
        }
    }

    private async Task<DirectorySearchAttempt> SearchWithTokenAsync(
        string name,
        Func<Task<string>> acquireToken,
        Func<GraphPeopleSearch, CancellationToken, Task<DirectorySearchAttempt>> run,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await acquireToken();
            var client = new GraphServiceClient(new StaticBearerTokenCredential(token));
            return await run(new GraphPeopleSearch(client, _logger), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Delegated Graph directory {Query} failed. Code={Code}.", name, GraphPeopleQueries.ErrorCode(ex) ?? "(none)");
            return DirectorySearchAttempt.Fail(name, ex);
        }
    }

    private static void AddAttempt(
        DirectorySearchAttempt attempt,
        HashSet<string> seen,
        List<DirectoryPrincipalDto> merged,
        List<Exception> errors)
    {
        AddRange(attempt.Results, seen, merged);
        if (attempt.Error is not null)
        {
            errors.Add(attempt.Error);
        }
    }

    private static void AddRange(
        IEnumerable<Intranet.Api.KnowledgeBase.Models.DirectoryPrincipalDto> remote,
        HashSet<string> seen,
        List<DirectoryPrincipalDto> merged)
    {
        foreach (var item in remote)
        {
            if (!seen.Add(item.Oid))
            {
                continue;
            }

            merged.Add(new DirectoryPrincipalDto(item.Type, item.Oid, item.DisplayName, item.Email, null));
        }
    }

    private static GraphDirectoryException ToDirectoryException(Exception ex) =>
        ex as GraphDirectoryException
        ?? new GraphDirectoryException(
            string.IsNullOrWhiteSpace(GraphPeopleQueries.ErrorCode(ex))
                ? ex.Message
                : $"{ex.Message} (Graph {GraphPeopleQueries.ErrorCode(ex)})",
            StatusCodes.Status502BadGateway,
            GraphPeopleQueries.ErrorCode(ex));
}
