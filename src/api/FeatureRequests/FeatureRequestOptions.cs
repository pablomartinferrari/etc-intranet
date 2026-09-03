namespace Intranet.Api.FeatureRequests;

/// <summary>
/// Feature request workflow settings. Bind from FeatureRequests__* App Settings.
/// Never commit a real approver mailbox, SMTP password, or Graph secret.
/// </summary>
public sealed class FeatureRequestOptions
{
    public const string SectionName = "FeatureRequests";

    public const string DefaultPublicBaseUrl = "https://intranet.2etc.com";

    /// <summary>
    /// Comma or semicolon separated approver emails.
    /// Normalized to trim + lowercase when read via <see cref="GetApproverEmails"/>.
    /// </summary>
    public string? ApproverEmails { get; set; }

    /// <summary>
    /// Absolute intranet origin used in notification links, e.g. https://intranet.2etc.com
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public FeatureRequestEmailOptions Email { get; set; } = new();

    public IReadOnlyList<string> GetApproverEmails() =>
        FeatureRequestAuthorization.ParseApproverEmails(ApproverEmails);

    public string ResolvedPublicBaseUrl
    {
        get
        {
            var value = PublicBaseUrl?.Trim().TrimEnd('/');
            return string.IsNullOrWhiteSpace(value) ? DefaultPublicBaseUrl : value;
        }
    }
}
