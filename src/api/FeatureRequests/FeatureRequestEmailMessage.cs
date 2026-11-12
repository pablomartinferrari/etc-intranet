using System.Net;
using System.Text;

namespace Intranet.Api.FeatureRequests;

public static class FeatureRequestEmailMessage
{
    public static (string Subject, string Text, string Html) FormatNew(
        FeatureRequestDto ticket,
        string publicBaseUrl)
    {
        var area = FeatureRequestPages.DisplayName(ticket.Page, ticket.AreaLabel);
        var link = RequestsUrl(publicBaseUrl);
        var title = string.IsNullOrWhiteSpace(ticket.Title) ? "New request" : ticket.Title.Trim();
        var requester = string.IsNullOrWhiteSpace(ticket.CreatedBy) ? "someone" : ticket.CreatedBy.Trim();
        var subject = $"ETC feature request #{ticket.Id}: {title}";
        var text = new StringBuilder()
            .AppendLine("A new feature request is awaiting approval.")
            .AppendLine()
            .AppendLine($"Id: {ticket.Id}")
            .AppendLine($"Title: {title}")
            .AppendLine($"Area: {area}")
            .AppendLine($"Requester: {requester}")
            .AppendLine()
            .AppendLine($"Review the queue: {link}")
            .ToString();
        var html =
            "<p>A new feature request is awaiting approval.</p>"
            + "<ul>"
            + $"<li><strong>Id:</strong> {Encode(ticket.Id.ToString())}</li>"
            + $"<li><strong>Title:</strong> {Encode(title)}</li>"
            + $"<li><strong>Area:</strong> {Encode(area)}</li>"
            + $"<li><strong>Requester:</strong> {Encode(requester)}</li>"
            + "</ul>"
            + $"<p><a href=\"{Encode(link)}\">Open Feature Requests</a></p>";
        return (subject, text, html);
    }

    public static string RequestsUrl(string publicBaseUrl)
    {
        var origin = string.IsNullOrWhiteSpace(publicBaseUrl)
            ? FeatureRequestOptions.DefaultPublicBaseUrl
            : publicBaseUrl.Trim().TrimEnd('/');
        return origin + "/requests";
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
