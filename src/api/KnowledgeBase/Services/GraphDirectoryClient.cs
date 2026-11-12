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
    private const int SearchTop = 8;
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
        var escaped = q.Replace("'", "''", StringComparison.Ordinal);
        var client = CreateClient();

        try
        {
            var usersTask = client.Users.GetAsync(config =>
            {
                config.QueryParameters.Filter =
                    $"startswith(displayName,'{escaped}') or startswith(mail,'{escaped}') or startswith(userPrincipalName,'{escaped}')";
                config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName"];
                config.QueryParameters.Top = SearchTop;
            }, cancellationToken);

            var groupsTask = client.Groups.GetAsync(config =>
            {
                config.QueryParameters.Filter = $"startswith(displayName,'{escaped}')";
                config.QueryParameters.Select = ["id", "displayName", "mail"];
                config.QueryParameters.Top = SearchTop;
            }, cancellationToken);

            await Task.WhenAll(usersTask, groupsTask);

            var results = new List<DirectoryPrincipalDto>();
            foreach (var user in usersTask.Result?.Value ?? [])
            {
                if (string.IsNullOrWhiteSpace(user.Id))
                {
                    continue;
                }

                results.Add(new DirectoryPrincipalDto(
                    KbProjectRoles.User,
                    user.Id,
                    user.DisplayName ?? user.UserPrincipalName ?? user.Id,
                    FirstEmail(user.Mail, user.UserPrincipalName)));
            }

            foreach (var group in groupsTask.Result?.Value ?? [])
            {
                if (string.IsNullOrWhiteSpace(group.Id))
                {
                    continue;
                }

                results.Add(new DirectoryPrincipalDto(
                    KbProjectRoles.Group,
                    group.Id,
                    group.DisplayName ?? group.Id,
                    FirstEmail(group.Mail)));
            }

            return results;
        }
        catch (Exception ex) when (ex is not GraphDirectoryException and not OperationCanceledException)
        {
            throw MapGraphError(ex, "Could not search Microsoft Entra users and groups.");
        }
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
        _logger.LogWarning(ex, "Graph directory call failed: {Message}", fallback);
        if (ex is ODataError odata)
        {
            var detail = odata.Error?.Message ?? fallback;
            return new GraphDirectoryException(detail, StatusCodes.Status502BadGateway);
        }

        return new GraphDirectoryException(fallback, StatusCodes.Status502BadGateway);
    }

    private static string? FirstEmail(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
