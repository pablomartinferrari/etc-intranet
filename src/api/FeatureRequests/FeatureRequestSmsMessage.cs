namespace Intranet.Api.FeatureRequests;

public static class FeatureRequestSmsMessage
{
    public const int MaxLength = 300;

    public static string Format(FeatureRequestDto ticket)
    {
        var area = FeatureRequestPages.DisplayName(ticket.Page, ticket.AreaLabel);
        var title = FirstMeaningful(ticket.Title, ticket.RawText);
        var who = string.IsNullOrWhiteSpace(ticket.CreatedBy) ? "someone" : ticket.CreatedBy.Trim();
        var text =
            $"ETC request #{ticket.Id} ({area}): {title}. From {who}. Review in Requests on Home.";
        return Truncate(text, MaxLength);
    }

    public static string FormatApproved(FeatureRequestDto ticket)
    {
        var area = FeatureRequestPages.DisplayName(ticket.Page, ticket.AreaLabel);
        var title = FirstMeaningful(ticket.Title, ticket.RawText);
        var text = $"ETC request #{ticket.Id} approved — ready to build. {title} ({area}).";
        return Truncate(text, MaxLength);
    }

    private static string FirstMeaningful(string title, string rawText)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return Truncate(title.Trim().ReplaceLineEndings(" "), 80);
        }

        var note = rawText.Trim().ReplaceLineEndings(" ");
        return string.IsNullOrWhiteSpace(note) ? "New request" : Truncate(note, 80);
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text[..Math.Max(0, max - 1)].TrimEnd() + "…";
    }
}
