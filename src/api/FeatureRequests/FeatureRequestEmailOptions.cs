namespace Intranet.Api.FeatureRequests;

/// <summary>
/// Email notify-on-create for feature requests.
/// Bind from FeatureRequests__Email__* env / App Settings / user secrets.
/// Never commit a real mailbox, SMTP password, or Graph secret.
/// </summary>
public sealed class FeatureRequestEmailOptions
{
    public const string SectionName = "FeatureRequests:Email";

    public const string ProviderGraph = "Graph";

    public const string ProviderSmtp = "Smtp";

    /// <summary>
    /// When false, skip sending even if credentials are present. Defaults to true;
    /// send still requires FromAddress and a configured provider.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Mailbox used as From. For Graph this is the user UPN /sendMail is called on.
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>Graph (default, reuses AzureAd client credentials) or Smtp.</summary>
    public string Provider { get; set; } = ProviderGraph;

    public FeatureRequestSmtpOptions Smtp { get; set; } = new();

    public bool IsGraph =>
        string.Equals(Provider, ProviderGraph, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(Provider);

    public bool IsSmtp =>
        string.Equals(Provider, ProviderSmtp, StringComparison.OrdinalIgnoreCase);

    public bool HasFromAddress => !string.IsNullOrWhiteSpace(FromAddress);
}

public sealed class FeatureRequestSmtpOptions
{
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseSsl { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && Port > 0;
}
