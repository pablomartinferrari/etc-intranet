namespace Intranet.Api.FeatureRequests;

/// <summary>
/// Twilio SMS notify-on-save for feature requests.
/// Bind from FeatureRequests__Sms__* env / App Settings / user secrets.
/// Never commit a real phone number, SID, or token.
/// </summary>
public sealed class FeatureRequestSmsOptions
{
    public const string SectionName = "FeatureRequests:Sms";

    /// <summary>
    /// When false, skip sending even if credentials are present. Defaults to true;
    /// send still requires To, From, AccountSid, and AuthToken.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Destination in E.164, e.g. +15555550100.</summary>
    public string? ToPhoneNumber { get; set; }

    /// <summary>Twilio from-number in E.164.</summary>
    public string? FromPhoneNumber { get; set; }

    public string? AccountSid { get; set; }

    public string? AuthToken { get; set; }

    public bool IsConfigured =>
        Enabled
        && HasValue(ToPhoneNumber)
        && HasValue(FromPhoneNumber)
        && HasValue(AccountSid)
        && HasValue(AuthToken);

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
