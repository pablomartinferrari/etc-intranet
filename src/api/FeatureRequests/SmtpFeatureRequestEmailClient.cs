using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Options;

namespace Intranet.Api.FeatureRequests;

public sealed class SmtpFeatureRequestEmailClient(IOptions<FeatureRequestEmailOptions> options)
    : IFeatureRequestEmailClient
{
    private readonly FeatureRequestEmailOptions _options = options.Value;

    public bool IsConfigured =>
        _options.Enabled
        && _options.IsSmtp
        && _options.HasFromAddress
        && _options.Smtp.IsConfigured;

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

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress!.Trim()),
            Subject = subject,
            Body = string.IsNullOrWhiteSpace(htmlBody) ? textBody : htmlBody,
            IsBodyHtml = !string.IsNullOrWhiteSpace(htmlBody),
        };
        if (!string.IsNullOrWhiteSpace(htmlBody) && !string.IsNullOrWhiteSpace(textBody))
        {
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(textBody, null, MediaTypeNames.Text.Plain));
        }

        foreach (var address in recipients)
        {
            message.To.Add(address);
        }

        using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
        {
            EnableSsl = _options.Smtp.UseSsl,
        };
        if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
        {
            client.Credentials = new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
