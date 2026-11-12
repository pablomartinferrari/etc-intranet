using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Intranet.Api.Agentic.Day;

internal sealed record GraphMailQueryResult(
    string Name,
    IReadOnlyList<Message> Messages,
    Exception? Error,
    bool UsedClientSideFilter = false);

internal sealed record GraphMailLoadResult(
    GraphMailQueryResult Flagged,
    GraphMailQueryResult Inbox,
    GraphMailQueryResult Sent)
{
    public bool MailboxUnreachable =>
        Flagged.Error is not null
        && Inbox.Error is not null
        && Flagged.Messages.Count == 0
        && Inbox.Messages.Count == 0;

    public Exception? FirstError => Flagged.Error ?? Inbox.Error ?? Sent.Error;
}

/// <summary>
/// Issues Graph mail queries independently so a flag-filter ErrorInefficientFilter
/// cannot hide awaiting-reply items, and vice versa.
/// </summary>
internal sealed class GraphMailLoader
{
    private readonly GraphServiceClient _client;
    private readonly ILogger _logger;

    public GraphMailLoader(GraphServiceClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<GraphMailLoadResult> LoadAsync(string sinceIso, CancellationToken cancellationToken)
    {
        var flaggedTask = TryAsync("flagged", () => LoadFlaggedAsync(cancellationToken), cancellationToken);
        var inboxTask = TryAsync("inbox", () => LoadInboxAsync(sinceIso, cancellationToken), cancellationToken);
        var sentTask = TryAsync("sent", () => LoadSentAsync(sinceIso, cancellationToken), cancellationToken);
        await Task.WhenAll(flaggedTask, inboxTask, sentTask);

        var flagged = flaggedTask.Result;
        var inbox = inboxTask.Result;
        var sent = sentTask.Result;

        if (flagged.Error is not null)
        {
            flagged = await RecoverFlaggedAsync(flagged, inbox, sinceIso, cancellationToken);
        }

        return new GraphMailLoadResult(
            flagged with { Messages = SortByReceived(flagged.Messages) },
            inbox,
            sent);
    }

    private async Task<GraphMailQueryResult> RecoverFlaggedAsync(
        GraphMailQueryResult failed,
        GraphMailQueryResult inbox,
        string sinceIso,
        CancellationToken cancellationToken)
    {
        if (GraphMailQueries.IsInefficientFilter(failed.Error!))
        {
            var inboxFlagged = await TryAsync(
                "flagged-inbox",
                () => LoadInboxFlaggedAsync(cancellationToken),
                cancellationToken);
            if (inboxFlagged.Error is null)
            {
                return inboxFlagged with { Name = "flagged" };
            }
        }

        if (inbox.Error is null)
        {
            return ClientSideFlagged(inbox.Messages, failed.Error);
        }

        var recent = await TryAsync(
            "flagged-fallback-inbox",
            () => LoadInboxAsync(sinceIso, cancellationToken),
            cancellationToken);
        if (recent.Error is null)
        {
            return ClientSideFlagged(recent.Messages, failed.Error);
        }

        return failed;
    }

    private GraphMailQueryResult ClientSideFlagged(IReadOnlyList<Message> messages, Exception? originalError)
    {
        var flagged = messages.Where(GraphMailQueries.IsFlagged).ToList();
        _logger.LogInformation(
            "Graph flagged-mail query failed (Code={Code}); filtered {FlaggedCount} flagged message(s) from {InboxCount} recent inbox message(s) client-side.",
            GraphMailQueries.ErrorCode(originalError!) ?? "(none)",
            flagged.Count,
            messages.Count);
        return new GraphMailQueryResult("flagged", flagged, Error: null, UsedClientSideFilter: true);
    }

    private async Task<GraphMailQueryResult> TryAsync(
        string name,
        Func<Task<List<Message>>> load,
        CancellationToken cancellationToken)
    {
        try
        {
            var messages = await load();
            return new GraphMailQueryResult(name, messages, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var code = GraphMailQueries.ErrorCode(ex);
            _logger.LogWarning(ex, "Graph mail {Query} failed. Code={Code}.", name, code ?? "(none)");
            return new GraphMailQueryResult(name, [], ex);
        }
    }

    private async Task<List<Message>> LoadFlaggedAsync(CancellationToken cancellationToken)
    {
        var page = await _client.Me.Messages.GetAsync(config =>
        {
            config.QueryParameters.Filter = GraphMailQueries.FlaggedFilter;
            config.QueryParameters.Select = GraphMailMapper.SelectFields;
            config.QueryParameters.Top = GraphMailQueries.FlaggedTop;
        }, cancellationToken);
        return page?.Value?.ToList() ?? [];
    }

    private async Task<List<Message>> LoadInboxFlaggedAsync(CancellationToken cancellationToken)
    {
        var page = await _client.Me.MailFolders["inbox"].Messages.GetAsync(config =>
        {
            config.QueryParameters.Filter = GraphMailQueries.FlaggedFilter;
            config.QueryParameters.Select = GraphMailMapper.SelectFields;
            config.QueryParameters.Top = GraphMailQueries.FlaggedTop;
        }, cancellationToken);
        return page?.Value?.ToList() ?? [];
    }

    private async Task<List<Message>> LoadInboxAsync(string sinceIso, CancellationToken cancellationToken)
    {
        var page = await _client.Me.MailFolders["inbox"].Messages.GetAsync(config =>
        {
            config.QueryParameters.Filter = GraphMailQueries.InboxFilter(sinceIso);
            config.QueryParameters.Select = GraphMailMapper.SelectFields;
            config.QueryParameters.Top = GraphMailQueries.InboxTop;
            config.QueryParameters.Orderby = GraphMailQueries.InboxOrderBy;
        }, cancellationToken);
        return page?.Value?.ToList() ?? [];
    }

    private async Task<List<Message>> LoadSentAsync(string sinceIso, CancellationToken cancellationToken)
    {
        var page = await _client.Me.MailFolders["sentitems"].Messages.GetAsync(config =>
        {
            config.QueryParameters.Filter = GraphMailQueries.SentFilter(sinceIso);
            config.QueryParameters.Select = GraphMailQueries.SentSelect;
            config.QueryParameters.Top = GraphMailQueries.SentTop;
            config.QueryParameters.Orderby = GraphMailQueries.SentOrderBy;
        }, cancellationToken);
        return page?.Value?.ToList() ?? [];
    }

    private static IReadOnlyList<Message> SortByReceived(IReadOnlyList<Message> messages) =>
        messages
            .OrderByDescending(m => m.ReceivedDateTime)
            .ToList();
}
