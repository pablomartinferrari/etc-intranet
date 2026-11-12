namespace Intranet.Api.FeatureRequests;

public interface IFeatureRequestLlm
{
    Task<string?> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
