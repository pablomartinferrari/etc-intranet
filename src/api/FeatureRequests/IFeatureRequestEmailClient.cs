namespace Intranet.Api.FeatureRequests;

public interface IFeatureRequestEmailClient
{
    bool IsConfigured { get; }

    Task SendAsync(
        IReadOnlyList<string> toAddresses,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken cancellationToken);
}
