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

    [Theory]
    [InlineData("chat")]
    [InlineData("lead")]
    [InlineData("general")]
    public async Task CreateAcceptsIntranetWideAreas(string page)
    {
        await using var db = CreateDb();
        var service = new FeatureRequestService(db, new NullLlm());

        var created = await service.CreateAsync(
            page,
            "The home cards are hard to scan on a phone.",
            "alex@etc.example",
            CancellationToken.None);

        Assert.Equal(page, created.Page);
        Assert.Equal("fallback", created.StructuredBy);
        Assert.Equal(page, Assert.Single(db.FeatureRequests).Page);
        Assert.False(string.IsNullOrWhiteSpace(created.DataInvolved));
        Assert.Contains(page, FeatureRequestStructurer.UserPrompt(page, "note"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRejectsUnknownAreaWithoutWriting()
    {
        await using var db = CreateDb();
        var service = new FeatureRequestService(db, new NullLlm());

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync("facilities", "Please add a parking map.", "alex@etc.example", CancellationToken.None));

        Assert.Contains("chat", error.Message, StringComparison.Ordinal);
        Assert.Contains("general", error.Message, StringComparison.Ordinal);
        Assert.Empty(db.FeatureRequests);
    }

    [Fact]
    public void FallbackKeepsLegacySalesPagesValid()
    {
        foreach (var page in new[] { "sales", "opportunities", "pipeline" })
        {
            var ticket = FeatureRequestStructurer.FromFallback(page, "Keep existing tickets working.");
            Assert.Equal("fallback", ticket.StructuredBy);
            Assert.False(string.IsNullOrWhiteSpace(ticket.DataInvolved));
            Assert.True(FeatureRequestPages.IsValid(page));
        }
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

    [Fact]
    public async Task CreateSendsSmsWhenConfigured()
    {
        await using var db = CreateDb();
        var sms = new RecordingSms(configured: true);
        var service = new FeatureRequestService(db, new NullLlm(), sms);

        var created = await service.CreateAsync(
            "chat",
            "Make the Chat export button easier to find.",
            "alex.rivera@etc.example",
            CancellationToken.None);

        var body = Assert.Single(sms.Messages);
        Assert.Contains($"#{created.Id}", body, StringComparison.Ordinal);
        Assert.Contains("Chat", body, StringComparison.Ordinal);
        Assert.Contains("Make the Chat export button easier to find.", body, StringComparison.Ordinal);
        Assert.Contains("alex.rivera@etc.example", body, StringComparison.Ordinal);
        Assert.Contains("Requests", body, StringComparison.Ordinal);
        Assert.True(body.Length <= FeatureRequestSmsMessage.MaxLength);
        Assert.Single(db.FeatureRequests);
    }

    [Fact]
    public async Task CreateSucceedsWhenSmsThrows()
    {
        await using var db = CreateDb();
        var service = new FeatureRequestService(db, new NullLlm(), new ThrowingSms());

        var created = await service.CreateAsync(
            "lead",
            "The XRF grid loses sort order after refresh.",
            "pablo@etc.example",
            CancellationToken.None);

        Assert.Equal("lead", created.Page);
        Assert.Equal("new", created.Status);
        Assert.Single(db.FeatureRequests);
    }

    [Fact]
    public async Task CreateSkipsSmsWhenNotConfigured()
    {
        await using var db = CreateDb();
        var sms = new RecordingSms(configured: false);
        var service = new FeatureRequestService(db, new NullLlm(), sms);

        await service.CreateAsync(
            "general",
            "The Home cards wrap poorly on a phone.",
            "alex@etc.example",
            CancellationToken.None);

        Assert.Empty(sms.Messages);
        Assert.Single(db.FeatureRequests);
    }

    [Fact]
    public async Task CreateDoesNotSendSmsWhenValidationFails()
    {
        await using var db = CreateDb();
        var sms = new RecordingSms(configured: true);
        var service = new FeatureRequestService(db, new NullLlm(), sms);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync("sales", "   ", "alex@etc.example", CancellationToken.None));

        Assert.Empty(sms.Messages);
        Assert.Empty(db.FeatureRequests);
    }

    [Fact]
    public void SmsMessageIsShortAndUsesAreaLabel()
    {
        var body = FeatureRequestSmsMessage.Format(new FeatureRequestDto
        {
            Id = 42,
            Page = "opportunities",
            CreatedBy = "alex@etc.example",
            CreatedAt = DateTimeOffset.UtcNow,
            RawText = new string('x', 500),
            Title = "NAICS filter on Bids",
            Problem = "too long to put in SMS",
            DesiredBehavior = "filter",
            DataInvolved = "GET /api/cleat/recommendations",
            AcceptanceCriteria = "lots of structured json that must not appear",
            Status = "new",
            StructuredBy = "llm",
        });

        Assert.Contains("Bids", body, StringComparison.Ordinal);
        Assert.Contains("#42", body, StringComparison.Ordinal);
        Assert.Contains("NAICS filter on Bids", body, StringComparison.Ordinal);
        Assert.Contains("alex@etc.example", body, StringComparison.Ordinal);
        Assert.DoesNotContain("too long to put in SMS", body, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/cleat/recommendations", body, StringComparison.Ordinal);
        Assert.True(body.Length <= FeatureRequestSmsMessage.MaxLength);
    }

    [Fact]
    public void SmsOptionsRequireDestinationAndTwilioCredentials()
    {
        var empty = new FeatureRequestSmsOptions();
        Assert.False(empty.IsConfigured);

        var ready = new FeatureRequestSmsOptions
        {
            Enabled = true,
            ToPhoneNumber = "+15555550100",
            FromPhoneNumber = "+15555550101",
            AccountSid = "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
            AuthToken = "placeholder-token",
        };
        Assert.True(ready.IsConfigured);

        ready.Enabled = false;
        Assert.False(ready.IsConfigured);
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

    private sealed class RecordingSms(bool configured) : IFeatureRequestSmsClient
    {
        public bool IsConfigured { get; } = configured;

        public List<string> Messages { get; } = [];

        public Task SendAsync(string body, CancellationToken cancellationToken)
        {
            Messages.Add(body);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSms : IFeatureRequestSmsClient
    {
        public bool IsConfigured => true;

        public Task SendAsync(string body, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Twilio is down.");
    }
}
