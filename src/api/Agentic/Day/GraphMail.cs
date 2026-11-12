using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using Intranet.Api.KnowledgeBase.Services;
using Intranet.Api.MultifamilyLbp.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Web;

namespace Intranet.Api.Agentic.Day;

public static class MailReadStates
{
    public const string Ok = "ok";
    public const string Mock = "mock";
    public const string ConsentRequired = "consent_required";
    public const string Unavailable = "unavailable";
    public const string Disabled = "disabled";
}

public sealed record MailFollowUp(
    string Id,
    string Subject,
    string? SenderName,
    string? SenderEmail,
    DateTimeOffset? Received,
    string? WebLink,
    string? BodyPreview,
    string Kind,
    string? NeedFromYou = null);

public sealed record MailDayResult(
    string State,
    string? Message,
    IReadOnlyList<MailFollowUp> Items);

public interface IUserGraphMailClient
{
    bool IsEnabled { get; }

    Task<MailDayResult> GetFollowUpsAsync(DayQuery query, CancellationToken cancellationToken);
}

public sealed class MailConsentException : Exception
{
    public const string UserMessage =
        "Workspace needs permission to read your Outlook mail. " +
        "An admin must grant the API app the delegated Microsoft Graph permission Mail.Read " +
        "(admin consent on the intranet web API app registration), then sign out and back in. " +
        "Missing Mail.Read does not affect Your day calendar.";

    public MailConsentException()
        : base(UserMessage)
    {
    }

    public MailConsentException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Follow-up sources (documented, keep the heuristic simple):
/// 1. Flagged messages: <c>GET /me/messages?$filter=flag/flagStatus eq 'flagged'&amp;$select=...&amp;$top=25</c>
///    with <b>no $orderby</b> (Graph ErrorInefficientFilter if flag filter is combined with
///    $orderby or extra properties). Sorted client-side. If that is rejected, try the same
///    filter on <c>/me/mailFolders/inbox/messages</c>; if still rejected, take recent inbox
///    messages and keep those with <c>flag/flagStatus == flagged</c> client-side.
/// 2. Inbox messages from the last <see cref="InboxLookbackDays"/> days:
///    <c>GET /me/mailFolders/inbox/messages?$filter=receivedDateTime ge {iso}&amp;$orderby=receivedDateTime desc</c>
///    (the $orderby property is first in $filter). To-line / not-from-me / not-draft checks
///    run client-side. Sent Items:
///    <c>GET /me/mailFolders/sentitems/messages?$filter=sentDateTime ge {iso}&amp;$orderby=sentDateTime desc</c>,
///    matched on <c>conversationId</c> client-side (never conversationId+date in $filter).
///    Each sub-query fails independently. Only metadata + <c>bodyPreview</c> are read.
/// </summary>
public static class OutlookFollowUpHeuristic
{
    public const int InboxLookbackDays = 3;
    public const int MaxItems = 20;

    public static bool IsFromUser(string? fromEmail, string? userEmail) =>
        EmailsEqual(fromEmail, userEmail);

    public static bool IsAddressedDirectlyToUser(IEnumerable<string?> toEmails, string? userEmail)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return false;
        }

        return toEmails.Any(address => EmailsEqual(address, userEmail));
    }

    public static bool HasUserReply(
        string? conversationId,
        DateTimeOffset received,
        IReadOnlyDictionary<string, DateTimeOffset> latestSentByConversation)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return false;
        }

        return latestSentByConversation.TryGetValue(conversationId, out var sent) && sent > received;
    }

    public static bool AwaitsReply(
        string? fromEmail,
        IEnumerable<string?> toEmails,
        string? userEmail,
        bool isDraft,
        string? conversationId,
        DateTimeOffset received,
        IReadOnlyDictionary<string, DateTimeOffset> latestSentByConversation)
    {
        if (isDraft || IsFromUser(fromEmail, userEmail) || !IsAddressedDirectlyToUser(toEmails, userEmail))
        {
            return false;
        }

        return !HasUserReply(conversationId, received, latestSentByConversation);
    }

    public static IReadOnlyList<MailFollowUp> Merge(
        IEnumerable<MailFollowUp> flagged,
        IEnumerable<MailFollowUp> awaiting)
    {
        var merged = new List<MailFollowUp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in flagged.Concat(awaiting))
        {
            if (string.IsNullOrWhiteSpace(item.Id) || !seen.Add(item.Id))
            {
                continue;
            }

            merged.Add(item);
            if (merged.Count >= MaxItems)
            {
                break;
            }
        }

        return merged
            .OrderByDescending(i => i.Kind == "flagged")
            .ThenByDescending(i => i.Received)
            .ToList();
    }

    public static bool EmailsEqual(string? left, string? right)
    {
        var a = NormalizeEmail(left);
        var b = NormalizeEmail(right);
        return a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        try
        {
            return new MailAddress(trimmed).Address;
        }
        catch (FormatException)
        {
            return trimmed;
        }
    }
}

/// <summary>
/// Graph mail query shapes that avoid ErrorInefficientFilter.
/// Combining $filter and $orderby requires the $orderby properties to appear in $filter
/// first, in the same order. <c>flag/flagStatus</c> must not be combined with $orderby
/// or extra $filter properties.
/// </summary>
public static class GraphMailQueries
{
    public const string FlaggedFilter = "flag/flagStatus eq 'flagged'";
    public const int FlaggedTop = 25;
    public const int InboxTop = 50;
    public const int SentTop = 100;

    public static readonly string[] InboxOrderBy = ["receivedDateTime desc"];
    public static readonly string[] SentOrderBy = ["sentDateTime desc"];
    public static readonly string[] SentSelect = ["conversationId", "sentDateTime"];

    public static string InboxFilter(string sinceIso) => $"receivedDateTime ge {sinceIso}";

    public static string SentFilter(string sinceIso) => $"sentDateTime ge {sinceIso}";

    public static string OrderByProperty(string orderByClause)
    {
        var property = orderByClause.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        return property;
    }

    public static bool OrderByPropertyIsFirstInFilter(string filter, string orderByClause)
    {
        var property = OrderByProperty(orderByClause);
        var trimmed = filter.TrimStart();
        if (!trimmed.StartsWith(property, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return trimmed.Length == property.Length || !char.IsLetterOrDigit(trimmed[property.Length]);
    }

    public static bool IsFlagged(Message message) =>
        message.Flag?.FlagStatus == FollowupFlagStatus.Flagged;

    public static string? ErrorCode(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is ODataError odata && !string.IsNullOrWhiteSpace(odata.Error?.Code))
            {
                return odata.Error.Code;
            }
        }

        return null;
    }

    public static bool IsInefficientFilter(Exception ex)
    {
        var code = ErrorCode(ex);
        if (string.Equals(code, "ErrorInefficientFilter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "InefficientFilter", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (ex.Message ?? "").Contains(
            "restriction or sort order is too complex",
            StringComparison.OrdinalIgnoreCase);
    }
}

public static class GraphMailMapper
{
    public static readonly string[] SelectFields =
    [
        "id", "subject", "from", "toRecipients", "receivedDateTime", "sentDateTime",
        "webLink", "bodyPreview", "flag", "conversationId", "isDraft",
    ];

    public static MailFollowUp? ToFollowUp(Message message, string kind)
    {
        if (string.IsNullOrWhiteSpace(message.Id) || message.IsDraft == true)
        {
            return null;
        }

        var from = message.From?.EmailAddress;
        var preview = message.BodyPreview?.Trim();
        if (preview is { Length: > 280 })
        {
            preview = preview[..277] + "...";
        }

        return new MailFollowUp(
            message.Id,
            string.IsNullOrWhiteSpace(message.Subject) ? "(no subject)" : message.Subject.Trim(),
            string.IsNullOrWhiteSpace(from?.Name) ? from?.Address : from!.Name,
            OutlookFollowUpHeuristic.NormalizeEmail(from?.Address),
            message.ReceivedDateTime,
            message.WebLink,
            string.IsNullOrWhiteSpace(preview) ? null : preview,
            kind);
    }

    public static IReadOnlyList<string> ToAddresses(Message message) =>
        (message.ToRecipients ?? [])
            .Select(r => OutlookFollowUpHeuristic.NormalizeEmail(r.EmailAddress?.Address))
            .Where(a => a is not null)
            .Cast<string>()
            .ToList();

    public static bool IsConsentError(Exception ex)
    {
        if (ex is MailConsentException
            or MicrosoftIdentityWebChallengeUserException
            or Microsoft.Identity.Client.MsalUiRequiredException)
        {
            return true;
        }

        if (ex.InnerException is not null && ex.InnerException != ex && IsConsentError(ex.InnerException))
        {
            return true;
        }

        return GraphCalendarMapper.IsConsentError(ex)
            || LooksLikeMailConsent(ex.Message);
    }

    private static bool LooksLikeMailConsent(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("Mail.Read", StringComparison.OrdinalIgnoreCase)
            || message.Contains("AADSTS65001", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Reads the signed-in user's mailbox with a dedicated Mail.Read OBO token.
/// Never queries another mailbox. Does not persist mail bodies.
/// </summary>
public sealed class GraphUserMailClient : IUserGraphMailClient
{
    private readonly IUserGraphTokenProvider _tokens;
    private readonly AgenticOptions _options;
    private readonly AzureAdOptions _azureAd;
    private readonly IConfiguration _configuration;
    private readonly ChatCompletionRouter _chat;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GraphUserMailClient> _logger;

    public GraphUserMailClient(
        IUserGraphTokenProvider tokens,
        IOptions<AgenticOptions> options,
        IOptions<AzureAdOptions> azureAd,
        IConfiguration configuration,
        ChatCompletionRouter chat,
        IMemoryCache cache,
        ILogger<GraphUserMailClient> logger)
    {
        _tokens = tokens;
        _options = options.Value;
        _azureAd = azureAd.Value;
        _configuration = configuration;
        _chat = chat;
        _cache = cache;
        _logger = logger;
    }

    public bool IsEnabled =>
        _options.UseGraphMail
        && EntraConfidentialClient.IsConfigured(_azureAd, _configuration)
        && _tokens.IsConfigured;

    public async Task<MailDayResult> GetFollowUpsAsync(DayQuery query, CancellationToken cancellationToken)
    {
        if (!_options.UseGraphMail)
        {
            return new MailDayResult(
                MailReadStates.Disabled,
                "Your day follow-ups are turned off (Features:AgenticWorkspace:UseGraphMail=false).",
                []);
        }

        if (!EntraConfidentialClient.IsConfigured(_azureAd, _configuration) || !_tokens.IsConfigured)
        {
            return new MailDayResult(
                MailReadStates.Unavailable,
                EntraConfidentialClient.MissingReason(_azureAd, _configuration)
                    ?? "Outlook Graph is not available.",
                []);
        }

        if (query.Actor.IsDemoIdentity)
        {
            return new MailDayResult(MailReadStates.Mock, null, []);
        }

        try
        {
            var accessToken = await _tokens.AcquireMailTokenAsync(cancellationToken);
            var client = new GraphServiceClient(new StaticBearerTokenCredential(accessToken));
            var since = query.Now.ToUniversalTime().AddDays(-OutlookFollowUpHeuristic.InboxLookbackDays);
            var sinceIso = since.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            var loaded = await new GraphMailLoader(client, _logger).LoadAsync(sinceIso, cancellationToken);
            if (loaded.MailboxUnreachable)
            {
                var error = loaded.FirstError!;
                if (error is MailConsentException || GraphMailMapper.IsConsentError(error))
                {
                    _logger.LogInformation(error, "Mail.Read consent missing for {Oid}.", query.Actor.Oid);
                    return new MailDayResult(MailReadStates.ConsentRequired, MailConsentException.UserMessage, []);
                }

                return new MailDayResult(
                    MailReadStates.Unavailable,
                    GraphFailureMessage(error),
                    []);
            }

            var latestSent = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
            foreach (var sent in loaded.Sent.Messages)
            {
                if (string.IsNullOrWhiteSpace(sent.ConversationId) || sent.SentDateTime is null)
                {
                    continue;
                }

                if (!latestSent.TryGetValue(sent.ConversationId, out var existing) || sent.SentDateTime > existing)
                {
                    latestSent[sent.ConversationId] = sent.SentDateTime.Value;
                }
            }

            var userEmail = query.Actor.Email;
            var flagged = loaded.Flagged.Messages
                .Select(m => GraphMailMapper.ToFollowUp(m, "flagged"))
                .Where(i => i is not null)
                .Cast<MailFollowUp>()
                .ToList();
            var awaiting = loaded.Inbox.Messages
                .Where(m => OutlookFollowUpHeuristic.AwaitsReply(
                    OutlookFollowUpHeuristic.NormalizeEmail(m.From?.EmailAddress?.Address),
                    GraphMailMapper.ToAddresses(m),
                    userEmail,
                    m.IsDraft == true,
                    m.ConversationId,
                    m.ReceivedDateTime ?? query.Now,
                    latestSent))
                .Select(m => GraphMailMapper.ToFollowUp(m, "awaiting-reply"))
                .Where(i => i is not null)
                .Cast<MailFollowUp>()
                .ToList();

            var merged = OutlookFollowUpHeuristic.Merge(flagged, awaiting);
            var withNeed = await AttachNeedLinesAsync(merged, cancellationToken);
            return new MailDayResult(MailReadStates.Ok, null, withNeed);
        }
        catch (Exception ex) when (ex is MailConsentException || GraphMailMapper.IsConsentError(ex))
        {
            _logger.LogInformation(ex, "Mail.Read consent missing for {Oid}.", query.Actor.Oid);
            return new MailDayResult(MailReadStates.ConsentRequired, MailConsentException.UserMessage, []);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Delegated Graph mail follow-ups failed for {Oid}.", query.Actor.Oid);
            return new MailDayResult(
                MailReadStates.Unavailable,
                GraphFailureMessage(ex),
                []);
        }
    }

    private async Task<IReadOnlyList<MailFollowUp>> AttachNeedLinesAsync(
        IReadOnlyList<MailFollowUp> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var result = items.ToList();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(6));
            var missing = new List<(int Index, MailFollowUp Item)>();
            for (var i = 0; i < result.Count; i++)
            {
                var cacheKey = $"agentic:mail-need:{result[i].Id}";
                if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
                {
                    result[i] = result[i] with { NeedFromYou = cached };
                }
                else
                {
                    missing.Add((i, result[i]));
                }
            }

            if (missing.Count == 0)
            {
                return result;
            }

            var payload = missing.Select(m => new
            {
                m.Item.Id,
                m.Item.Subject,
                from = m.Item.SenderName ?? m.Item.SenderEmail,
                preview = m.Item.BodyPreview,
            });
            var generated = await _chat.CompleteAsync(
                "For each email, write one short line (max 12 words) of what they need from the recipient. " +
                "Use only subject and bodyPreview. Return JSON array of {id, need}. No extra text.",
                JsonSerializer.Serialize(payload),
                cts.Token);
            using var doc = JsonDocument.Parse(ExtractJsonArray(generated.Content));
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var id = element.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var need = element.TryGetProperty("need", out var needProp) ? needProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(need))
                {
                    continue;
                }

                var index = result.FindIndex(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    continue;
                }

                var trimmed = need.Trim();
                if (trimmed.Length > 120)
                {
                    trimmed = trimmed[..117] + "...";
                }

                result[index] = result[index] with { NeedFromYou = trimmed };
                _cache.Set($"agentic:mail-need:{id}", trimmed, TimeSpan.FromMinutes(15));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "Mail need-from-you lines skipped; showing subject/preview only.");
        }

        return result;
    }

    private static string ExtractJsonArray(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "[]";
        }

        var start = content.IndexOf('[');
        var end = content.LastIndexOf(']');
        return start >= 0 && end > start ? content[start..(end + 1)] : "[]";
    }

    private static string GraphFailureMessage(Exception ex)
    {
        var code = GraphMailQueries.ErrorCode(ex);
        var detail = ex is ODataError odata
            ? odata.Error?.Message ?? odata.Message
            : ex.Message;
        if (detail.Length > 180)
        {
            detail = detail[..177] + "...";
        }

        var inner = string.IsNullOrWhiteSpace(code) ? detail : code + ": " + detail;
        return "Outlook mail is temporarily unavailable (" + inner + "). Follow-ups are hidden rather than showing sample items.";
    }
}
