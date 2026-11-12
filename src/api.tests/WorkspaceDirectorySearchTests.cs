using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Day;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using KbDirectory = Intranet.Api.KnowledgeBase.Models.DirectoryPrincipalDto;

namespace Intranet.Api.Tests;

public class WorkspaceDirectorySearchTests
{
    [Fact]
    public async Task SearchThrowsWhenGraphIsNotConfiguredAndCatalogIsEmpty()
    {
        var directory = CreateDirectory(graph: new StubGraph(), tokensConfigured: false, demo: false);

        var ex = await Assert.ThrowsAsync<GraphDirectoryException>(() =>
            directory.SearchAsync("ad", CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ex.StatusCode);
        Assert.Contains("User.ReadBasic.All", ex.Message, StringComparison.Ordinal);
        Assert.Contains("User.Read.All", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShortQueryDoesNotCallDirectoryAndDoesNotThrow()
    {
        var graph = new StubGraph { IsConfigured = true };
        var directory = CreateDirectory(graph, tokensConfigured: false, demo: false);

        var results = await directory.SearchAsync("a", CancellationToken.None);

        Assert.Empty(results);
        Assert.Equal(0, graph.SearchCalls);
    }

    [Fact]
    public async Task GraphFailureSurfacesGraphErrorCodeInsteadOfEmptyList()
    {
        var graph = new StubGraph
        {
            IsConfigured = true,
            Error = new GraphDirectoryException(
                "Directory search denied",
                StatusCodes.Status502BadGateway,
                "Authorization_RequestDenied"),
        };
        var directory = CreateDirectory(graph, tokensConfigured: false, demo: false);

        var ex = await Assert.ThrowsAsync<GraphDirectoryException>(() =>
            directory.SearchAsync("ada", CancellationToken.None));

        Assert.Equal("Authorization_RequestDenied", ex.GraphErrorCode);
        Assert.Contains("Directory search denied", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppOnlyResultsAreReturnedWhenDelegatedIsOff()
    {
        var graph = new StubGraph
        {
            IsConfigured = true,
            Results =
            [
                new KbDirectory("user", "oid-ada", "Ada Lovelace", "ada@etc.local"),
            ],
        };
        var directory = CreateDirectory(graph, tokensConfigured: false, demo: false);

        var results = await directory.SearchAsync("ada", CancellationToken.None);

        Assert.Contains(results, p => p.Oid == "oid-ada" && p.Email == "ada@etc.local");
        Assert.Equal(1, graph.SearchCalls);
    }

    [Fact]
    public async Task EmailQueryIsAllowedBelowTwoLettersWhenItLooksLikeAnAddress()
    {
        var graph = new StubGraph { IsConfigured = true, Results = [] };
        var directory = CreateDirectory(graph, tokensConfigured: false, demo: false);

        var results = await directory.SearchAsync("a@b.co", CancellationToken.None);

        Assert.Equal(1, graph.SearchCalls);
        Assert.Empty(results);
    }

    private static WorkspaceDirectory CreateDirectory(StubGraph graph, bool tokensConfigured, bool demo) =>
        new(
            graph,
            new StubTokens { IsConfigured = tokensConfigured },
            Options.Create(new AgenticOptions
            {
                ShareDemoCatalog = demo,
                AllowAnonymous = demo,
            }),
            NullLogger<WorkspaceDirectory>.Instance);

    private sealed class StubTokens : IUserGraphTokenProvider
    {
        public bool IsConfigured { get; set; }

        public Task<string> AcquireCalendarTokenAsync(CancellationToken cancellationToken) => Task.FromResult("t");

        public Task<string> AcquireMailTokenAsync(CancellationToken cancellationToken) => Task.FromResult("t");

        public Task<string> AcquireDirectoryUserTokenAsync(CancellationToken cancellationToken) => Task.FromResult("t");

        public Task<string> AcquireDirectoryGroupTokenAsync(CancellationToken cancellationToken) => Task.FromResult("t");
    }

    private sealed class StubGraph : IGraphDirectoryClient
    {
        public bool IsConfigured { get; set; }

        public IReadOnlyList<KbDirectory> Results { get; set; } = [];

        public GraphDirectoryException? Error { get; set; }

        public int SearchCalls { get; private set; }

        public Task<IReadOnlyList<KbDirectory>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            SearchCalls++;
            if (Error is not null)
            {
                throw Error;
            }

            return Task.FromResult(Results);
        }

        public Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
            string userOid,
            IReadOnlyCollection<string> groupOids,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }
}
