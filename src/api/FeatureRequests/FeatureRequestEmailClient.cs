using Microsoft.Extensions.Options;

namespace Intranet.Api.FeatureRequests;

/// <summary>
/// Routes feature-request mail to Graph (default) or SMTP based on FeatureRequests:Email:Provider.
/// </summary>
public sealed class FeatureRequestEmailClient(
    IOptions<FeatureRequestEmailOptions> options,
    GraphFeatureRequestEmailClient graph,
    SmtpFeatureRequestEmailClient smtp) : IFeatureRequestEmailClient
{
    private readonly IFeatureRequestEmailClient _inner = Resolve(options.Value, graph, smtp);

    public bool IsConfigured => _inner.IsConfigured;

    public Task SendAsync(
        IReadOnlyList<string> toAddresses,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken cancellationToken) =>
        _inner.SendAsync(toAddresses, subject, textBody, htmlBody, cancellationToken);

    private static IFeatureRequestEmailClient Resolve(
        FeatureRequestEmailOptions options,
        GraphFeatureRequestEmailClient graph,
        SmtpFeatureRequestEmailClient smtp) =>
        options.IsSmtp ? smtp : graph;
}
