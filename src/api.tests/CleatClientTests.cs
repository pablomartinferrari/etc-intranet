using System.Net;
using System.Text;
using Intranet.Api.Cleat;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Tests;

public class CleatClientTests
{
    [Fact]
    public async Task MissingApiKeyThrowsWithoutCallingHttp()
    {
        var handler = new StubHandler();
        var client = CreateClient(handler, apiKey: null);

        var ex = await Assert.ThrowsAsync<CleatNotConfiguredException>(
            () => client.GetRecommendationsAsync(80, 10, CancellationToken.None));

        Assert.Contains("Cleat__ApiKey", ex.Message, StringComparison.Ordinal);
        Assert.Null(handler.LastRequest);
        Assert.False(client.HasApiKey);
    }

    [Fact]
    public async Task SendsApiKeyHeaderAndMinScoreQuery()
    {
        var handler = new StubHandler
        {
            Response = JsonResponse("""{ "items": [ { "id": "contract_1", "title": "Test" } ] }"""),
        };
        var client = CreateClient(handler, apiKey: "unit-test-placeholder-key");

        var result = await client.GetRecommendationsAsync(80, 20, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("unit-test-placeholder-key", handler.LastRequest!.Headers.GetValues("X-Api-Key").Single());
        Assert.Contains("min_score=80", handler.LastRequest.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal("contract_1", Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task UnauthorizedBecomesUpstreamErrorWithoutLeakingKey()
    {
        var handler = new StubHandler { Response = new HttpResponseMessage(HttpStatusCode.Unauthorized) };
        var client = CreateClient(handler, apiKey: "unit-test-placeholder-key");

        var ex = await Assert.ThrowsAsync<CleatUpstreamException>(
            () => client.GetRecommendationsAsync(80, 10, CancellationToken.None));

        Assert.Equal("cleat_unauthorized", ex.ErrorCode);
        Assert.DoesNotContain("unit-test-placeholder-key", ex.Message, StringComparison.Ordinal);
        Assert.Equal(502, ex.StatusCode);
    }

    [Fact]
    public void RejectsUnsafeOpportunityIds()
    {
        Assert.False(CleatClient.IsValidOpportunityId("../secret"));
        Assert.False(CleatClient.IsValidOpportunityId("id with spaces"));
        Assert.False(CleatClient.IsValidOpportunityId(""));
        Assert.True(CleatClient.IsValidOpportunityId("contract_abc"));
        Assert.True(CleatClient.IsValidOpportunityId("forecast_1"));
        Assert.True(CleatClient.IsValidOpportunityId("pur_xyz"));
    }

    private static CleatClient CreateClient(StubHandler handler, string? apiKey)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.cleat.ai/") };
        var options = Options.Create(new CleatOptions { ApiKey = apiKey, BaseUrl = "https://api.cleat.ai" });
        return new CleatClient(http, options, NullLogger<CleatClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }
}
