using Intranet.Api.Agentic.Day;
using Intranet.Api.MultifamilyLbp.Options;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Intranet.Api.Tests;

public class OutlookFollowUpHeuristicTests
{
    [Fact]
    public void AwaitsReplyRequiresDirectToAndNoLaterSentItem()
    {
        var sent = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase)
        {
            ["conv-1"] = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero),
        };
        var received = new DateTimeOffset(2026, 11, 4, 11, 0, 0, TimeSpan.Zero);

        Assert.True(OutlookFollowUpHeuristic.AwaitsReply(
            "ada@etc.local",
            ["pat@etc.local"],
            "pat@etc.local",
            isDraft: false,
            "conv-new",
            received,
            sent));
        Assert.False(OutlookFollowUpHeuristic.AwaitsReply(
            "ada@etc.local",
            ["other@etc.local"],
            "pat@etc.local",
            false,
            "conv-new",
            received,
            sent));
        Assert.False(OutlookFollowUpHeuristic.AwaitsReply(
            "pat@etc.local",
            ["ada@etc.local"],
            "pat@etc.local",
            false,
            "conv-new",
            received,
            sent));
        Assert.False(OutlookFollowUpHeuristic.AwaitsReply(
            "ada@etc.local",
            ["pat@etc.local"],
            "pat@etc.local",
            false,
            "conv-1",
            received,
            sent));
    }

    [Fact]
    public void MergePrefersFlaggedAndDeduplicates()
    {
        var flagged = new MailFollowUp(
            "1",
            "Flagged",
            "Ada",
            "ada@etc.local",
            DateTimeOffset.UtcNow,
            "https://outlook.office.com/1",
            "preview",
            "flagged");
        var awaiting = flagged with { Kind = "awaiting-reply", Subject = "Also inbox" };
        var other = flagged with { Id = "2", Subject = "Please reply", Kind = "awaiting-reply" };

        var merged = OutlookFollowUpHeuristic.Merge([flagged], [awaiting, other]);
        Assert.Equal(2, merged.Count);
        Assert.Equal("1", merged[0].Id);
        Assert.Equal("flagged", merged[0].Kind);
        Assert.Equal("2", merged[1].Id);
    }

    [Fact]
    public void EntraConfidentialClientReportsMissingSecret()
    {
        var azureAd = new AzureAdOptions
        {
            TenantId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            ClientId = "bbbbbbbb-cccc-dddd-eeee-ffffffffffff",
            ClientSecret = "",
        };
        Assert.False(EntraConfidentialClient.IsConfigured(azureAd, new ConfigurationBuilder().Build()));
        Assert.Contains("ClientSecret", EntraConfidentialClient.MissingReason(azureAd), StringComparison.Ordinal);

        azureAd.ClientSecret = "secret";
        Assert.True(EntraConfidentialClient.IsConfigured(azureAd));
        Assert.Null(EntraConfidentialClient.MissingReason(azureAd));
    }

    [Fact]
    public void CalendarAndMailUseSeparateOboScopes()
    {
        Assert.Equal(["https://graph.microsoft.com/Calendars.Read"], ObOUserGraphTokenProvider.CalendarScopes);
        Assert.Equal(["https://graph.microsoft.com/Mail.Read"], ObOUserGraphTokenProvider.MailScopes);
        Assert.NotEqual(ObOUserGraphTokenProvider.CalendarScopes, ObOUserGraphTokenProvider.MailScopes);
    }
}
