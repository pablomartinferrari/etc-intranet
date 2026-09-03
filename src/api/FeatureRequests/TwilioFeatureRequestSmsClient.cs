using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;

namespace Intranet.Api.FeatureRequests;

public sealed class TwilioFeatureRequestSmsClient(
    HttpClient http,
    IOptions<FeatureRequestSmsOptions> options) : IFeatureRequestSmsClient
{
    public bool IsConfigured => options.Value.IsConfigured;

    public async Task SendAsync(string body, CancellationToken cancellationToken)
    {
        var cfg = options.Value;
        if (!cfg.IsConfigured)
        {
            return;
        }

        var sid = cfg.AccountSid!.Trim();
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"2010-04-01/Accounts/{Uri.EscapeDataString(sid)}/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{sid}:{cfg.AuthToken!.Trim()}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = cfg.ToPhoneNumber!.Trim(),
            ["From"] = cfg.FromPhoneNumber!.Trim(),
            ["Body"] = body,
        });

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            if (detail.Length > 400)
            {
                detail = detail[..400];
            }

            throw new HttpRequestException(
                $"Twilio SMS failed ({(int)response.StatusCode}): {detail}");
        }
    }
}
