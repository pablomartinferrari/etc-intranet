using Intranet.Api.Agentic;
using Microsoft.Graph.Models.ODataErrors;
using Xunit;

namespace Intranet.Api.Tests;

public class GraphPeopleQueryTests
{
    [Fact]
    public void UserSearchUsesDisplayNameAndMailAnywhere()
    {
        Assert.Equal(2, GraphPeopleQueries.MinQueryLength);
        Assert.Equal(8, GraphPeopleQueries.Top);
        Assert.Equal("\"displayName:Ada\" OR \"mail:Ada\"", GraphPeopleQueries.UserSearch("Ada"));
        Assert.Equal("\"displayName:Ada\"", GraphPeopleQueries.GroupSearch("Ada"));
    }

    [Fact]
    public void FilterFallbackUsesStartswithOnNameMailAndUpn()
    {
        Assert.Equal(
            "startswith(displayName,'Ada') or startswith(mail,'Ada') or startswith(userPrincipalName,'Ada')",
            GraphPeopleQueries.UserFilter("Ada"));
        Assert.Equal("startswith(displayName,'Ada')", GraphPeopleQueries.GroupFilter("Ada"));
        Assert.Equal("startswith(displayName,'O''Brien')", GraphPeopleQueries.GroupFilter("O'Brien"));
    }

    [Fact]
    public void EmailQueriesBypassMinLengthAndLookLikeEmail()
    {
        Assert.True(GraphPeopleQueries.LooksLikeEmail("ada@etc.local"));
        Assert.False(GraphPeopleQueries.LooksLikeEmail("a"));
        Assert.False(GraphPeopleQueries.LooksLikeEmail("not-an-email"));
    }

    [Fact]
    public void SearchFallbackTriggersOnUnsupportedQueryCodes()
    {
        var error = new ODataError { Error = new() { Code = "Request_UnsupportedQuery", Message = "$search" } };
        Assert.True(GraphPeopleQueries.ShouldFallBackFromSearch(error));
        Assert.Equal("Request_UnsupportedQuery", GraphPeopleQueries.ErrorCode(error));
    }
}
