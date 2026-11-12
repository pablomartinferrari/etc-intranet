using Azure.Identity;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.MultifamilyLbp.Options;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Users.Item.CheckMemberGroups;

namespace Intranet.Api.KnowledgeBase.Services;

public sealed class GraphDirectoryClient : IGraphDirectoryClient
{
    private const int CheckMemberBatch = 20;

    private readonly AzureAdOptions _azureAd;
    private readonly ILogger<GraphDirectoryClient> _logger;

    public GraphDirectoryClient(IOptions<AzureAdOptions> azureAd, ILogger<GraphDirectoryClient> logger)
    {
        _azureAd = azureAd.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        HasValue(_azureAd.TenantId)
        && !string.Equals(_azureAd.TenantId, "common", StringComparison.OrdinalIgnoreCase)
        && HasValue(_azureAd.ClientId)
        && !_azureAd.ClientId.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
        && HasValue(_azureAd.ClientSecret);

    public async Task<IReadOnlyList<DirectoryPrincipalDto>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var q = query.Trim();
        if (q.Length == 0)
        {
            return [];
        }

        EnsureConfigured();
        q = q.Length > 64 ? q[..64] : q;
        var search = new Intranet.Api.Agentic.GraphPeopleSearch(CreateClient(), _logger);
        var usersTask = search.SearchUsersAsync(q, cancellationToken);
        var groupsTask = search.SearchGroupsAsync(q, cancellationToken);
        await Task.WhenAll(usersTask, groupsTask);

        var results = new List<DirectoryPrincipalDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Exception? error = null;
        foreach (var attempt in new[] { usersTask.Result, groupsTask.Result })
        {
            foreach (var item in attempt.Results)
            {
                if (seen.Add(item.Oid))
                {
                    results.Add(item);
                }
            }

            error ??= attempt.Error;
        }

        if (Intranet.Api.Agentic.GraphPeopleQueries.LooksLikeEmail(q)
            && results.All(p => !string.Equals(p.Email, q, StringComparison.OrdinalIgnoreCase)))
        {
            var resolved = await search.ResolveUserAsync(q, cancellationToken);
            foreach (var item in resolved.Results)
            {
                if (seen.Add(item.Oid))
                {
                    results.Add(item);
                }
            }

            error ??= resolved.Error;
        }

        if (results.Count == 0 && error is not null)
        {
            throw MapGraphError(error, "Could not search Microsoft Entra users and groups.");
        }

        return results;
    }

    public async Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
        string userOid,
        IReadOnlyCollection<string> groupOids,
        CancellationToken cancellationToken)
    {
        var distinct = groupOids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0 || string.IsNullOrWhiteSpace(userOid) || !IsConfigured)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var client = CreateClient();

        try
        {
            for (var i = 0; i < distinct.Count; i += CheckMemberBatch)
            {
                var batch = distinct.Skip(i).Take(CheckMemberBatch).ToList();
                var body = new CheckMemberGroupsPostRequestBody { GroupIds = batch };
                var response = await client.Users[userOid].CheckMemberGroups
                    .PostAsCheckMemberGroupsPostResponseAsync(body, cancellationToken: cancellationToken);
                foreach (var id in response?.Value ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        matched.Add(id);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Graph checkMemberGroups failed for user {UserOid}; group shares will be skipped.", userOid);
        }

        return matched;
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

        throw new GraphDirectoryException(
            "Microsoft Graph is not configured for directory search. " +
            "Set AzureAd__TenantId, AzureAd__ClientId, and AzureAd__ClientSecret, then grant application permissions " +
            "User.Read.All and Group.Read.All (or GroupMember.Read.All) with admin consent.",
            StatusCodes.Status503ServiceUnavailable);
    }

    private GraphDirectoryException MapGraphError(Exception ex, string fallback)
    {
        var code = Intranet.Api.Agentic.GraphPeopleQueries.ErrorCode(ex);
        _logger.LogWarning(ex, "Graph directory call failed. Code={Code}. {Message}", code ?? "(none)", fallback);
        if (ex is GraphDirectoryException existing)
        {
            return existing;
        }

        var detail = ex is ODataError odata ? odata.Error?.Message ?? fallback : ex.Message;
        var message = string.IsNullOrWhiteSpace(code) ? detail : $"{detail} (Graph {code})";
        return new GraphDirectoryException(message, StatusCodes.Status502BadGateway, code);
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
