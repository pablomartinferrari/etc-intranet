using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using KbDirectory = Intranet.Api.KnowledgeBase.Models.DirectoryPrincipalDto;

namespace Intranet.Api.Agentic;

/// <summary>
/// Executes Graph people/group search against a given <see cref="GraphServiceClient"/>
/// (delegated OBO or app-only). Users and groups fail independently.
/// </summary>
internal sealed class GraphPeopleSearch
{
    private readonly GraphServiceClient _client;
    private readonly ILogger _logger;

    public GraphPeopleSearch(GraphServiceClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<DirectorySearchAttempt> SearchUsersAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _client.Users.GetAsync(config =>
            {
                config.QueryParameters.Search = GraphPeopleQueries.UserSearch(query);
                config.QueryParameters.Select = GraphPeopleQueries.UserSelect;
                config.QueryParameters.Top = GraphPeopleQueries.Top;
                config.QueryParameters.Count = true;
                config.Headers.Add("ConsistencyLevel", "eventual");
            }, cancellationToken);
            return DirectorySearchAttempt.Ok("users-search", MapUsers(page?.Value));
        }
        catch (Exception ex) when (ex is not OperationCanceledException && GraphPeopleQueries.ShouldFallBackFromSearch(ex))
        {
            Log(ex, "users-search");
            return await FilterUsersAsync(query, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(ex, "users-search");
            return DirectorySearchAttempt.Fail("users-search", ex);
        }
    }

    public async Task<DirectorySearchAttempt> FilterUsersAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _client.Users.GetAsync(config =>
            {
                config.QueryParameters.Filter = GraphPeopleQueries.UserFilter(query);
                config.QueryParameters.Select = GraphPeopleQueries.UserSelect;
                config.QueryParameters.Top = GraphPeopleQueries.Top;
            }, cancellationToken);
            return DirectorySearchAttempt.Ok("users-filter", MapUsers(page?.Value));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(ex, "users-filter");
            return DirectorySearchAttempt.Fail("users-filter", ex);
        }
    }

    public async Task<DirectorySearchAttempt> SearchGroupsAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _client.Groups.GetAsync(config =>
            {
                config.QueryParameters.Search = GraphPeopleQueries.GroupSearch(query);
                config.QueryParameters.Select = GraphPeopleQueries.GroupSelect;
                config.QueryParameters.Top = GraphPeopleQueries.Top;
                config.QueryParameters.Count = true;
                config.Headers.Add("ConsistencyLevel", "eventual");
            }, cancellationToken);
            return DirectorySearchAttempt.Ok("groups-search", MapGroups(page?.Value));
        }
        catch (Exception ex) when (ex is not OperationCanceledException && GraphPeopleQueries.ShouldFallBackFromSearch(ex))
        {
            Log(ex, "groups-search");
            return await FilterGroupsAsync(query, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(ex, "groups-search");
            return DirectorySearchAttempt.Fail("groups-search", ex);
        }
    }

    public async Task<DirectorySearchAttempt> FilterGroupsAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _client.Groups.GetAsync(config =>
            {
                config.QueryParameters.Filter = GraphPeopleQueries.GroupFilter(query);
                config.QueryParameters.Select = GraphPeopleQueries.GroupSelect;
                config.QueryParameters.Top = GraphPeopleQueries.Top;
            }, cancellationToken);
            return DirectorySearchAttempt.Ok("groups-filter", MapGroups(page?.Value));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(ex, "groups-filter");
            return DirectorySearchAttempt.Fail("groups-filter", ex);
        }
    }

    public async Task<DirectorySearchAttempt> ResolveUserAsync(string upnOrEmail, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _client.Users[upnOrEmail.Trim()].GetAsync(config =>
            {
                config.QueryParameters.Select = GraphPeopleQueries.UserSelect;
            }, cancellationToken);
            if (user is null || string.IsNullOrWhiteSpace(user.Id))
            {
                return DirectorySearchAttempt.Ok("users-resolve", []);
            }

            return DirectorySearchAttempt.Ok("users-resolve", [MapUser(user)]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(ex, "users-resolve");
            return DirectorySearchAttempt.Fail("users-resolve", ex);
        }
    }

    private void Log(Exception ex, string query)
    {
        var code = GraphPeopleQueries.ErrorCode(ex);
        _logger.LogWarning(ex, "Graph directory {Query} failed. Code={Code}.", query, code ?? "(none)");
    }

    private static List<KbDirectory> MapUsers(IEnumerable<User>? users) =>
        (users ?? []).Where(u => !string.IsNullOrWhiteSpace(u.Id)).Select(MapUser).ToList();

    private static KbDirectory MapUser(User user) =>
        new(
            KbProjectRoles.User,
            user.Id!,
            user.DisplayName ?? user.UserPrincipalName ?? user.Mail ?? user.Id!,
            FirstEmail(user.Mail, user.UserPrincipalName));

    private static List<KbDirectory> MapGroups(IEnumerable<Group>? groups) =>
        (groups ?? [])
            .Where(g => !string.IsNullOrWhiteSpace(g.Id))
            .Select(g => new KbDirectory(
                KbProjectRoles.Group,
                g.Id!,
                g.DisplayName ?? g.Id!,
                FirstEmail(g.Mail)))
            .ToList();

    private static string? FirstEmail(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

internal sealed record DirectorySearchAttempt(
    string Name,
    IReadOnlyList<KbDirectory> Results,
    Exception? Error)
{
    public bool Succeeded => Error is null;

    public static DirectorySearchAttempt Ok(string name, IReadOnlyList<KbDirectory> results) =>
        new(name, results, null);

    public static DirectorySearchAttempt Fail(string name, Exception error) =>
        new(name, [], error);
}
