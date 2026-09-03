namespace Intranet.Api.FeatureRequests;

public interface IFeatureRequestSmsClient
{
    bool IsConfigured { get; }

    Task SendAsync(string body, CancellationToken cancellationToken);
}
