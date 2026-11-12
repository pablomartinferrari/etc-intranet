using Intranet.Api.Agentic.Day;
using Microsoft.Graph.Models.ODataErrors;
using Xunit;

namespace Intranet.Api.Tests;

public class GraphMailQueryTests
{
    private const string SinceIso = "2026-11-09T00:00:00Z";

    [Fact]
    public void FlaggedQueryHasFilterAndTopAndNoOrderBy()
    {
        Assert.Equal("flag/flagStatus eq 'flagged'", GraphMailQueries.FlaggedFilter);
        Assert.Equal(25, GraphMailQueries.FlaggedTop);
        Assert.DoesNotContain("orderby", GraphMailQueries.FlaggedFilter, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("receivedDateTime", GraphMailQueries.FlaggedFilter, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "/me/messages?$filter=flag/flagStatus eq 'flagged'&$top=25",
            "/me/messages?$filter=" + GraphMailQueries.FlaggedFilter + "&$top=" + GraphMailQueries.FlaggedTop);
    }

    [Fact]
    public void InboxQueryOrdersByTheFilterPropertyFirst()
    {
        var filter = GraphMailQueries.InboxFilter(SinceIso);
        Assert.Equal($"receivedDateTime ge {SinceIso}", filter);
        Assert.Equal(["receivedDateTime desc"], GraphMailQueries.InboxOrderBy);
        Assert.Equal(50, GraphMailQueries.InboxTop);
        Assert.True(GraphMailQueries.OrderByPropertyIsFirstInFilter(filter, GraphMailQueries.InboxOrderBy[0]));
        Assert.Equal(
            "/me/mailFolders/inbox/messages?$filter=" + filter + "&$orderby=" + GraphMailQueries.InboxOrderBy[0] + "&$top=" + GraphMailQueries.InboxTop,
            "/me/mailFolders/inbox/messages?$filter=receivedDateTime ge " + SinceIso + "&$orderby=receivedDateTime desc&$top=50");
    }

    [Fact]
    public void SentQuerySelectsConversationIdAndOrdersBySentDateTimeFirst()
    {
        var filter = GraphMailQueries.SentFilter(SinceIso);
        Assert.Equal($"sentDateTime ge {SinceIso}", filter);
        Assert.Equal(["sentDateTime desc"], GraphMailQueries.SentOrderBy);
        Assert.Equal(["conversationId", "sentDateTime"], GraphMailQueries.SentSelect);
        Assert.Equal(100, GraphMailQueries.SentTop);
        Assert.True(GraphMailQueries.OrderByPropertyIsFirstInFilter(filter, GraphMailQueries.SentOrderBy[0]));
        Assert.Equal(
            "/me/mailFolders/sentitems/messages?$filter=" + filter + "&$orderby=" + GraphMailQueries.SentOrderBy[0] +
            "&$select=" + string.Join(",", GraphMailQueries.SentSelect) + "&$top=" + GraphMailQueries.SentTop,
            "/me/mailFolders/sentitems/messages?$filter=sentDateTime ge " + SinceIso +
            "&$orderby=sentDateTime desc&$select=conversationId,sentDateTime&$top=100");
    }

    [Fact]
    public void FlaggedFilterPlusOrderByWouldNotSatisfyOrderByFirstRule()
    {
        Assert.False(GraphMailQueries.OrderByPropertyIsFirstInFilter(
            GraphMailQueries.FlaggedFilter,
            "receivedDateTime desc"));
    }

    [Fact]
    public void DetectsInefficientFilterFromODataErrorCode()
    {
        var error = new ODataError
        {
            Error = new() { Code = "ErrorInefficientFilter", Message = "restriction or sort order is too complex" },
        };
        Assert.Equal("ErrorInefficientFilter", GraphMailQueries.ErrorCode(error));
        Assert.True(GraphMailQueries.IsInefficientFilter(error));
    }
}
