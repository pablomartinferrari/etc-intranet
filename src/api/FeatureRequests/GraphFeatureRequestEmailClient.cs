using Azure.Identity;
using Intranet.Api.MultifamilyLbp.Options;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace Intranet.Api.FeatureRequests;

/// <summary>
/// Sends mail via Microsoft Graph /users/{from}/sendMail using the same
/// AzureAd client-credential stack as SharePoint.
/// Requires application permission Mail.Send and a mailbox in FromAddress.
/// </summary>
public sealed class GraphFeatureRequestEmailClient(
    IOptions<FeatureRequestEmailOptions> options,
    IOptions<AzureAdOptions> azureAd) : IFeatureRequestEmailClient
{
    private readonly FeatureRequestEmailOptions _options = options.Value;
    private readonly AzureAdOptions _azureAd = azureAd.Value;

    public bool IsConfigured =>
        _options.Enabled
        && _options.IsGraph
        && _options.HasFromAddress
        && HasValue(_azureAd.TenantId)
        && HasValue(_azureAd.ClientId)
        && HasValue(_azureAd.ClientSecret);

    public async Task SendAsync(
        IReadOnlyList<string> toAddresses,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return;
        }

        var recipients = toAddresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        var credential = new ClientSecretCredential(
            _azureAd.TenantId,
            _azureAd.ClientId,
            _azureAd.ClientSecret);
        var client = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
        var body = new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = string.IsNullOrWhiteSpace(htmlBody) ? textBody : htmlBody,
                },
                ToRecipients = recipients
                    .Select(address => new Recipient
                    {
                        EmailAddress = new EmailAddress { Address = address },
                    })
                    .ToList(),
            },
            SaveToSentItems = false,
        };

        await client.Users[_options.FromAddress!.Trim()]
            .SendMail
            .PostAsync(body, cancellationToken: cancellationToken);
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
