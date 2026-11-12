using System.Globalization;
using Azure.Core;
using Intranet.Api.MultifamilyLbp.Options;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace Intranet.Api.Agentic.Day;

public static class CalendarReadStates
{
    public const string Ok = "ok";
    public const string Mock = "mock";
    public const string ConsentRequired = "consent_required";
    public const string Unavailable = "unavailable";
    public const string Disabled = "disabled";
}

public sealed record DayMeeting(
    string Id,
    string Subject,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    string? Organizer,
    IReadOnlyList<string> Attendees,
    string? Location,
    string? TeamsLink,
    string? BodyPreview,
    string? WebLink);

public sealed record CalendarDayResult(
    string State,
    string? Message,
    IReadOnlyList<DayMeeting> Meetings);

public interface IUserGraphCalendarClient
{
    bool IsEnabled { get; }

    Task<CalendarDayResult> GetTodaysMeetingsAsync(DayQuery query, CancellationToken cancellationToken);
}

public interface IUserGraphTokenProvider
{
    bool IsConfigured { get; }

    Task<string> AcquireCalendarTokenAsync(CancellationToken cancellationToken);
}

public sealed class CalendarConsentException : Exception
{
    public const string UserMessage =
        "Workspace needs permission to read your Outlook calendar. " +
        "An admin must grant the API app the delegated Microsoft Graph permission Calendars.Read " +
        "(admin consent), then sign out and back in.";

    public CalendarConsentException()
        : base(UserMessage)
    {
    }

    public CalendarConsentException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// On-behalf-of token for the signed-in user only. Never accepts another user's oid.
/// </summary>
public sealed class ObOUserGraphTokenProvider : IUserGraphTokenProvider
{
    public static readonly string[] CalendarScopes = ["https://graph.microsoft.com/Calendars.Read"];

    private readonly ITokenAcquisition _tokens;
    private readonly AzureAdOptions _azureAd;

    public ObOUserGraphTokenProvider(ITokenAcquisition tokens, IOptions<AzureAdOptions> azureAd)
    {
        _tokens = tokens;
        _azureAd = azureAd.Value;
    }

    public bool IsConfigured => EntraConfidentialClient.IsConfigured(_azureAd);

    public async Task<string> AcquireCalendarTokenAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Entra confidential client is not configured for Graph OBO.");
        }

        try
        {
            var tenant = string.IsNullOrWhiteSpace(_azureAd.TenantId) ||
                         string.Equals(_azureAd.TenantId, "common", StringComparison.OrdinalIgnoreCase)
                ? null
                : _azureAd.TenantId;
            return await _tokens.GetAccessTokenForUserAsync(
                CalendarScopes,
                tenantId: tenant,
                tokenAcquisitionOptions: new TokenAcquisitionOptions { CancellationToken = cancellationToken });
        }
        catch (Exception ex) when (GraphCalendarMapper.IsConsentError(ex))
        {
            throw new CalendarConsentException();
        }
    }
}

public static class EntraConfidentialClient
{
    public static bool IsConfigured(AzureAdOptions azureAd) =>
        HasValue(azureAd.TenantId)
        && !string.Equals(azureAd.TenantId, "common", StringComparison.OrdinalIgnoreCase)
        && HasValue(azureAd.ClientId)
        && !azureAd.ClientId.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
        && HasValue(azureAd.ClientSecret);

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}

/// <summary>
/// Reads <c>GET /me/calendarView</c> with the caller's delegated token.
/// Never queries <c>/users/{oid}</c> — that would be another mailbox.
/// </summary>
public sealed class GraphUserCalendarClient : IUserGraphCalendarClient
{
    private readonly IUserGraphTokenProvider _tokens;
    private readonly AgenticOptions _options;
    private readonly AzureAdOptions _azureAd;
    private readonly ILogger<GraphUserCalendarClient> _logger;

    public GraphUserCalendarClient(
        IUserGraphTokenProvider tokens,
        IOptions<AgenticOptions> options,
        IOptions<AzureAdOptions> azureAd,
        ILogger<GraphUserCalendarClient> logger)
    {
        _tokens = tokens;
        _options = options.Value;
        _azureAd = azureAd.Value;
        _logger = logger;
    }

    public bool IsEnabled =>
        _options.UseGraphCalendar
        && EntraConfidentialClient.IsConfigured(_azureAd)
        && _tokens.IsConfigured;

    public async Task<CalendarDayResult> GetTodaysMeetingsAsync(
        DayQuery query,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return new CalendarDayResult(CalendarReadStates.Disabled, null, []);
        }

        if (query.Actor.IsDemoIdentity)
        {
            return new CalendarDayResult(CalendarReadStates.Mock, null, []);
        }

        try
        {
            var accessToken = await _tokens.AcquireCalendarTokenAsync(cancellationToken);
            var client = new GraphServiceClient(new StaticBearerTokenCredential(accessToken));
            var timeZoneId = query.TimeZoneId;
            if (timeZoneId == DayTimeZones.Utc)
            {
                timeZoneId = await TryMailboxTimeZoneAsync(client, cancellationToken) ?? timeZoneId;
            }

            var tz = DayTimeZones.Resolve(timeZoneId);
            var start = DayTimeZones.StartOfDay(query.Date, tz);
            var end = DayTimeZones.ExclusiveEndOfDay(query.Date, tz);
            var page = await client.Me.CalendarView.GetAsync(config =>
            {
                config.QueryParameters.StartDateTime = start.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                config.QueryParameters.EndDateTime = end.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                config.QueryParameters.Select = GraphCalendarMapper.SelectFields;
                config.QueryParameters.Top = 50;
                config.QueryParameters.Orderby = ["start/dateTime"];
                config.Headers.Add("Prefer", $"outlook.timezone=\"{DayTimeZones.Normalize(timeZoneId)}\"");
            }, cancellationToken);

            var meetings = new List<DayMeeting>();
            foreach (var ev in page?.Value ?? [])
            {
                var mapped = GraphCalendarMapper.ToMeeting(ev);
                if (mapped is not null)
                {
                    meetings.Add(mapped);
                }
            }

            return new CalendarDayResult(CalendarReadStates.Ok, null, meetings);
        }
        catch (Exception ex) when (ex is CalendarConsentException || GraphCalendarMapper.IsConsentError(ex))
        {
            _logger.LogInformation(ex, "Calendar consent missing for {Oid}.", query.Actor.Oid);
            return new CalendarDayResult(CalendarReadStates.ConsentRequired, CalendarConsentException.UserMessage, []);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Delegated Graph calendarView failed for {Oid}.", query.Actor.Oid);
            return new CalendarDayResult(
                CalendarReadStates.Unavailable,
                "Outlook calendar is temporarily unavailable. Sample meetings are hidden so another person's data cannot appear here.",
                []);
        }
    }

    private static async Task<string?> TryMailboxTimeZoneAsync(GraphServiceClient client, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await client.Me.MailboxSettings.GetAsync(
                config => config.QueryParameters.Select = ["timeZone"],
                cancellationToken);
            var mailboxTz = settings?.TimeZone;
            if (string.IsNullOrWhiteSpace(mailboxTz))
            {
                return null;
            }

            var normalized = DayTimeZones.Normalize(mailboxTz);
            return normalized == DayTimeZones.Utc && !string.Equals(mailboxTz, "UTC", StringComparison.OrdinalIgnoreCase)
                ? null
                : normalized;
        }
        catch
        {
            return null;
        }
    }
}

public static class GraphCalendarMapper
{
    public static readonly string[] SelectFields =
    [
        "id", "subject", "start", "end", "organizer", "attendees", "location",
        "onlineMeeting", "onlineMeetingUrl", "webLink", "bodyPreview",
        "isOnlineMeeting", "isAllDay", "isCancelled",
    ];

    public static DayMeeting? ToMeeting(Event ev)
    {
        if (string.IsNullOrWhiteSpace(ev.Id) || ev.IsCancelled == true)
        {
            return null;
        }

        var start = ParseGraphTime(ev.Start);
        var end = ParseGraphTime(ev.End);
        var organizer = ev.Organizer?.EmailAddress?.Name
            ?? ev.Organizer?.EmailAddress?.Address;
        var attendees = (ev.Attendees ?? [])
            .Select(a => a.EmailAddress?.Name ?? a.EmailAddress?.Address)
            .Where(s => !string.IsNullOrWhiteSpace(s) && !string.Equals(s, organizer, StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
        var teams = FirstNonEmpty(ev.OnlineMeeting?.JoinUrl, ev.OnlineMeetingUrl);
        var location = ev.IsOnlineMeeting == true && string.IsNullOrWhiteSpace(ev.Location?.DisplayName)
            ? "Microsoft Teams"
            : ev.Location?.DisplayName;
        var preview = ev.BodyPreview?.Trim();
        if (preview is { Length: > 280 })
        {
            preview = preview[..277] + "...";
        }

        return new DayMeeting(
            ev.Id,
            string.IsNullOrWhiteSpace(ev.Subject) ? "Untitled meeting" : ev.Subject.Trim(),
            start,
            end,
            organizer,
            attendees,
            location,
            teams,
            string.IsNullOrWhiteSpace(preview) ? null : preview,
            ev.WebLink);
    }

    public static bool IsConsentError(Exception ex)
    {
        if (ex is CalendarConsentException
            or MicrosoftIdentityWebChallengeUserException
            or MsalUiRequiredException)
        {
            return true;
        }

        if (ex.InnerException is not null && ex.InnerException != ex && IsConsentError(ex.InnerException))
        {
            return true;
        }

        if (ex is ODataError odata)
        {
            var code = odata.Error?.Code ?? "";
            var message = odata.Error?.Message ?? odata.Message;
            if (code.Contains("auth", StringComparison.OrdinalIgnoreCase)
                || code.Contains("accessDenied", StringComparison.OrdinalIgnoreCase)
                || code.Contains("Authorization_RequestDenied", StringComparison.OrdinalIgnoreCase)
                || code.Contains("Forbidden", StringComparison.OrdinalIgnoreCase)
                || LooksLikeConsent(message))
            {
                return true;
            }
        }

        return LooksLikeConsent(ex.Message);
    }

    private static bool LooksLikeConsent(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("consent", StringComparison.OrdinalIgnoreCase)
            || message.Contains("AADSTS65001", StringComparison.OrdinalIgnoreCase)
            || message.Contains("AADSTS65005", StringComparison.OrdinalIgnoreCase)
            || message.Contains("AADSTS70011", StringComparison.OrdinalIgnoreCase)
            || message.Contains("insufficient privileges", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Calendars.Read", StringComparison.OrdinalIgnoreCase);
    }

    public static DateTimeOffset? ParseGraphTime(DateTimeTimeZone? value)
    {
        if (string.IsNullOrWhiteSpace(value?.DateTime))
        {
            return null;
        }

        if (!DateTime.TryParse(
                value.DateTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return DateTimeOffset.TryParse(value.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
                ? dto
                : null;
        }

        if (!string.IsNullOrWhiteSpace(value.TimeZone) && DayTimeZones.TryResolve(value.TimeZone, out var tz))
        {
            var unspecified = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
            return new DateTimeOffset(unspecified, tz.GetUtcOffset(unspecified));
        }

        return parsed.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(parsed, TimeSpan.Zero)
            : new DateTimeOffset(parsed);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

internal sealed class StaticBearerTokenCredential(string token) : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext context, CancellationToken cancellationToken) =>
        new(token, DateTimeOffset.UtcNow.AddMinutes(10));

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext context,
        CancellationToken cancellationToken) =>
        new(GetToken(context, cancellationToken));
}
