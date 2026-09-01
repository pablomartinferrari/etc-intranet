using System.Net;
using System.Text;
using Intranet.Api.Cleat;
using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class PipelineClientAndCloseoutTests
{
    [Fact]
    public async Task PipelineSearchMissingApiKeyThrowsWithoutHttp()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler, apiKey: null);

        var ex = await Assert.ThrowsAsync<CleatNotConfiguredException>(
            () => client.SearchPipelineAsync("active", null, 20, CancellationToken.None));

        Assert.Contains("Cleat__ApiKey", ex.Message, StringComparison.Ordinal);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task UpdatePursuitSendsColumnIdNotPhase()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            },
        };
        var client = CreateClient(handler, apiKey: "unit-test-placeholder-key");

        await client.UpdatePursuitAsync("pur_1", columnTitle: "Won", archived: null, CancellationToken.None);

        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Equal("/v1/pursuits/pur_1", handler.LastRequest.RequestUri!.AbsolutePath);
        var body = handler.LastBody!;
        Assert.Contains("\"column_id\":\"Won\"", body.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.DoesNotContain("phase", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CloseoutPersistsReasonWhenCleatusWriteFails()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler, apiKey: null);
        await using var db = CreateDb();
        var service = new PipelineService(client, db, NullLogger<PipelineService>.Instance);

        var result = await service.CloseOutAsync(
            "pur_stale",
            new CloseoutRequest { Outcome = "lost", ReasonCode = "capacity", Note = "Crew booked" },
            CancellationToken.None);

        Assert.False(result.CleatusUpdated);
        Assert.Equal("cleat_api_key_missing", result.Error);
        Assert.Contains("Cleat__ApiKey", result.Message, StringComparison.Ordinal);
        Assert.Null(handler.LastRequest);

        var stored = Assert.Single(db.PursuitCloseouts);
        Assert.Equal("pur_stale", stored.PursuitId);
        Assert.Equal("lost", stored.Outcome);
        Assert.Equal("capacity", stored.ReasonCode);
        Assert.Equal("Crew booked", stored.Note);
        Assert.Null(stored.CleatusSyncedAt);
    }

    [Fact]
    public async Task CloseoutPersistsAndMarksSyncedWhenPatchSucceeds()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            },
        };
        var client = CreateClient(handler, apiKey: "unit-test-placeholder-key");
        await using var db = CreateDb();
        var service = new PipelineService(client, db, NullLogger<PipelineService>.Instance);

        var result = await service.CloseOutAsync(
            "pur_win",
            new CloseoutRequest { Outcome = "won", ReasonCode = "past_performance" },
            CancellationToken.None);

        Assert.True(result.CleatusUpdated);
        Assert.NotNull(result.Closeout.CleatusSyncedAt);
        Assert.Contains("\"column_id\":\"Won\"", handler.LastBody!.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.Equal("won", Assert.Single(db.PursuitCloseouts).Outcome);
    }

    [Fact]
    public async Task LostWithoutReasonIsRejectedAndNotStored()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler, apiKey: "unit-test-placeholder-key");
        await using var db = CreateDb();
        var service = new PipelineService(client, db, NullLogger<PipelineService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CloseOutAsync(
                "pur_x",
                new CloseoutRequest { Outcome = "lost" },
                CancellationToken.None));

        Assert.Empty(db.PursuitCloseouts);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task DashboardMissingKeyDoesNotTouchDatabase()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler, apiKey: null);
        await using var db = CreateDb();
        db.PursuitCloseouts.Add(new PursuitCloseout
        {
            PursuitId = "pur_existing",
            Outcome = "won",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        var service = new PipelineService(client, db, NullLogger<PipelineService>.Instance);

        await Assert.ThrowsAsync<CleatNotConfiguredException>(
            () => service.GetDashboardAsync(CancellationToken.None));
        Assert.Null(handler.LastRequest);
        Assert.Equal(1, await db.PursuitCloseouts.CountAsync());
    }

    private static IntranetDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IntranetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IntranetDbContext(options);
    }

    private static CleatClient CreateClient(RecordingHandler handler, string? apiKey)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.cleat.ai/") };
        var options = Options.Create(new CleatOptions { ApiKey = apiKey, BaseUrl = "https://api.cleat.ai" });
        return new CleatClient(http, options, NullLogger<CleatClient>.Instance);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }
}
