using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Agentic.Realtime;

/// <summary>
/// Local-dev realtime for shared chats. Production can swap this hub's
/// transport for Azure Web PubSub for SignalR without changing the SPA
/// event names (presence, typing, message, agent).
/// </summary>
public sealed class WorkspaceHub : Hub
{
    private static readonly ConcurrentDictionary<string, PresenceRecord> Connections = new();

    private readonly IWorkspaceService _workspace;
    private readonly AgenticOptions _options;

    public WorkspaceHub(IWorkspaceService workspace, IOptions<AgenticOptions> options)
    {
        _workspace = workspace;
        _options = options.Value;
    }

    public static IReadOnlyList<PresenceUserDto> PresenceFor(Guid chatId) =>
        Connections.Values
            .Where(p => p.ChatId == chatId)
            .GroupBy(p => p.Oid, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                return new PresenceUserDto(first.Oid, first.DisplayName, Initial(first.DisplayName), g.Any(x => x.Typing));
            })
            .OrderBy(p => p.DisplayName)
            .ToList();

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Connections.TryRemove(Context.ConnectionId, out var record))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(record.ChatId));
            await Clients.Group(GroupName(record.ChatId)).SendAsync(
                "presence",
                PresenceFor(record.ChatId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinChat(Guid chatId)
    {
        var actor = WorkspaceActor.FromUser(Context.User ?? new System.Security.Claims.ClaimsPrincipal(), _options);
        _ = await _workspace.GetChatAsync(actor, chatId, Context.ConnectionAborted);
        if (Connections.TryGetValue(Context.ConnectionId, out var previous) && previous.ChatId != chatId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(previous.ChatId));
        }

        Connections[Context.ConnectionId] = new PresenceRecord(chatId, actor.Oid, actor.DisplayName, false);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(chatId));
        await Clients.Group(GroupName(chatId)).SendAsync("presence", PresenceFor(chatId));
    }

    public async Task Typing(Guid chatId, bool isTyping)
    {
        if (!Connections.TryGetValue(Context.ConnectionId, out var record) || record.ChatId != chatId)
        {
            return;
        }

        Connections[Context.ConnectionId] = record with { Typing = isTyping };
        await Clients.Group(GroupName(chatId)).SendAsync("presence", PresenceFor(chatId));
        await Clients.OthersInGroup(GroupName(chatId)).SendAsync(
            "typing",
            new { oid = record.Oid, displayName = record.DisplayName, typing = isTyping });
    }

    public const string RepositoryGroup = "repository";

    public static string GroupName(Guid chatId) => $"chat:{chatId:D}";

    public Task JoinRepository() =>
        Groups.AddToGroupAsync(Context.ConnectionId, RepositoryGroup);

    private static string Initial(string name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[0].ToString().ToUpperInvariant();

    private sealed record PresenceRecord(Guid ChatId, string Oid, string DisplayName, bool Typing);
}
