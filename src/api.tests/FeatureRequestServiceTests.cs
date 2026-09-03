using Intranet.Api.Data;
using Intranet.Api.FeatureRequests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Intranet.Api.Tests;

public class FeatureRequestServiceTests
{
    [Fact]
    public async Task CreatePersistsFallbackWhenLlmIsDown()
    {
        await using var db = CreateDb();
        var service = new FeatureRequestService(db, new NullLlm());

        var created = await service.CreateAsync(
            "pipeline",
            "Add a filter for NAICS on the pipeline list.\nOnly show environmental consulting codes.",
            "alex.rivera@etc.example",
            CancellationToken.None);

        Assert.Equal("pipeline", created.Page);
        Assert.Equal("alex.rivera@etc.example", created.CreatedBy);
        Assert.Equal("new", created.Status);
        Assert.Equal("fallback", created.StructuredBy);
        Assert.Equal("Add a filter for NAICS on the pipeline list.", created.Title);
        Assert.Equal("Only show environmental consulting codes.", created.Problem);
        Assert.Contains("GET /api/cleat/pipeline", created.DataInvolved, StringComparison.Ordinal);
        Assert.Equal("Add a filter for NAICS on the pipeline list.\nOnly show environmental consulting codes.", created.RawText);

        var stored = Assert.Single(db.FeatureRequests);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal("fallback", stored.StructuredBy);
    }

    [Fact]
    public async Task CreateUsesLlmJsonWhenAvailable()
    {
        await using var db = CreateDb();
        var llm = new ScriptedLlm(
            """
            {
              "title": "NAICS filter on Bids",
              "problem": "The recommendations table cannot be narrowed by NAICS.",
              "desiredBehavior": "Staff can pick a NAICS code and the list refreshes.",
              "dataInvolved": "GET /api/cleat/recommendations?minScore=80",
              "acceptanceCriteria": "- Add a NAICS query param\n- Keep the default min score at 80"
            }
            """);
        var service = new FeatureRequestService(db, llm);

        var created = await service.CreateAsync(
            "opportunities",
            "Please add a NAICS filter.",
            "oid-123",
            CancellationToken.None);

        Assert.Equal("llm", created.StructuredBy);
        Assert.Equal("NAICS filter on Bids", created.Title);
        Assert.Equal("The recommendations table cannot be narrowed by NAICS.", created.Problem);
        Assert.Equal("Staff can pick a NAICS code and the list refreshes.", created.DesiredBehavior);
        Assert.Equal("GET /api/cleat/recommendations?minScore=80", created.DataInvolved);
        Assert.Contains("Add a NAICS query param", created.AcceptanceCriteria, StringComparison.Ordinal);
        Assert.Equal("Please add a NAICS filter.", created.RawText);
        Assert.Contains("opportunities", llm.LastUserPrompt, StringComparison.Ordinal);
        Assert.Contains("GET /api/cleat/recommendations", llm.LastUserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateFallsBackWhenLlmReturnsGarbage()
    {
        await using var db = CreateDb();
        var service = new FeatureRequestService(db, new ScriptedLlm("not json at all"));

        var created = await service.CreateAsync(
            "sales",
            "Make the Requests link easier to find.",
            "pablo@etc.example",
            CancellationToken.None);

        Assert.Equal("fallback", created.StructuredBy);
        Assert.Equal("Make the Requests link easier to find.", created.Title);
        Assert.Single(db.FeatureRequests);
    }

    [Fact]
    public async Task CreateRejectsEmptyNoteWithoutWriting()
    {
        await using var db = CreateDb();
        var service = new FeatureRequestService(db, new NullLlm());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync("sales", "   ", "alex@etc.example", CancellationToken.None));

        Assert.Empty(db.FeatureRequests);
    }

    [Fact]
    public async Task ListIsNewestFirstAndStatusCanChange()
    {
        await using var db = CreateDb();
        var service = new FeatureRequestService(db, new NullLlm());

        var older = await service.CreateAsync("sales", "First note", "a@etc.example", CancellationToken.None);
        await Task.Delay(5);
        var newer = await service.CreateAsync("pipeline", "Second note", "b@etc.example", CancellationToken.None);

        var listed = await service.ListAsync(CancellationToken.None);
        Assert.Equal(newer.Id, listed[0].Id);
        Assert.Equal(older.Id, listed[1].Id);

        var updated = await service.UpdateStatusAsync(older.Id, "planned", CancellationToken.None);
        Assert.Equal("planned", updated!.Status);
        Assert.Equal("planned", (await service.ListAsync(CancellationToken.None)).Single(row => row.Id == older.Id).Status);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateStatusAsync(older.Id, "shipped", CancellationToken.None));
    }

    [Fact]
    public void FallbackUsesFirstEightyCharactersWhenSingleLine()
    {
        var text = new string('x', 120);
        var ticket = FeatureRequestStructurer.FromFallback("sales", text);
        Assert.Equal(80, ticket.Title.Length);
        Assert.Equal(text, ticket.Problem);
        Assert.Equal("fallback", ticket.StructuredBy);
    }

    private static IntranetDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IntranetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IntranetDbContext(options);
    }

    private sealed class NullLlm : IFeatureRequestLlm
    {
        public Task<string?> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class ScriptedLlm(string? reply) : IFeatureRequestLlm
    {
        public string LastUserPrompt { get; private set; } = string.Empty;

        public Task<string?> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            LastUserPrompt = userPrompt;
            return Task.FromResult(reply);
        }
    }
}
