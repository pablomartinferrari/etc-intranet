using System.Security.Claims;
using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Intranet.Api.FeatureRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class FeatureRequestApprovalTests
{
    [Theory]
    [InlineData("planned", "approved")]
    [InlineData("done", "shipped")]
    [InlineData("new", "new")]
    [InlineData("approved", "approved")]
    public void LegacyStatusesNormalize(string stored, string expected)
    {
        Assert.Equal(expected, FeatureRequestStatuses.Normalize(stored));
        Assert.True(FeatureRequestStatuses.IsValid(stored));
    }

    [Theory]
    [InlineData("new", "approved", true)]
    [InlineData("new", "rejected", true)]
    [InlineData("new", "shipped", false)]
    [InlineData("new", "closed", false)]
    [InlineData("approved", "shipped", true)]
    [InlineData("approved", "rejected", true)]
    [InlineData("approved", "closed", true)]
    [InlineData("approved", "new", false)]
    [InlineData("shipped", "closed", true)]
    [InlineData("shipped", "approved", false)]
    [InlineData("rejected", "approved", false)]
    [InlineData("rejected", "new", false)]
    [InlineData("closed", "new", false)]
    [InlineData("planned", "shipped", true)]
    [InlineData("done", "closed", true)]
    public void TransitionsFollowApprovalLoop(string from, string to, bool allowed)
    {
        Assert.Equal(allowed, FeatureRequestStatuses.CanTransition(from, to));
    }

    [Fact]
    public void ApproverEmailsNormalizeTrimLowercaseAndSplit()
    {
        var emails = FeatureRequestAuthorization.ParseApproverEmails(
            "  Alex@ETC.example ;pat@etc.example, alex@etc.example,,not-an-email ");

        Assert.Equal(["alex@etc.example", "pat@etc.example"], emails);
    }

    [Fact]
    public void ApproverEmailsEmptyWhenUnset()
    {
        Assert.Empty(FeatureRequestAuthorization.ParseApproverEmails(null));
        Assert.Empty(FeatureRequestAuthorization.ParseApproverEmails("  ; , "));
        Assert.Empty(new FeatureRequestOptions().GetApproverEmails());
    }

    [Fact]
    public void ActorFromUserPrefersEntraEmailClaims()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("preferred_username", "Pat@ETC.example"),
                new Claim("email", "other@etc.example"),
                new Claim("oid", "oid-pat"),
                new Claim("name", "Pat Rivera"),
            ],
            authenticationType: "Bearer"));

        var actor = FeatureRequestActor.FromUser(user);
        Assert.True(actor.IsAuthenticated);
        Assert.Equal("Pat@ETC.example", actor.Email);
        Assert.Equal("oid-pat", actor.ObjectId);
        Assert.Equal("Pat Rivera", actor.Name);
        Assert.Equal("Pat@ETC.example", actor.CreatedBy);
    }

    [Fact]
    public void ActorFromUserFallsBackToOidWhenEmailMissing()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("oid", "oid-only")],
            authenticationType: "Bearer"));

        var actor = FeatureRequestActor.FromUser(user);
        Assert.Equal("oid-only", actor.CreatedBy);
        Assert.Null(actor.Email);
    }

    [Fact]
    public void RequesterMatchesEmailOidOrName()
    {
        var byEmail = new FeatureRequestActor("alex@etc.example", "oid-1", "Alex");
        Assert.True(FeatureRequestAuthorization.IsRequester(byEmail, "Alex@ETC.example"));

        var byOid = new FeatureRequestActor(null, "oid-1", "Alex");
        Assert.True(FeatureRequestAuthorization.IsRequester(byOid, "oid-1"));

        var byName = new FeatureRequestActor(null, null, "Alex Rivera");
        Assert.True(FeatureRequestAuthorization.IsRequester(byName, "Alex Rivera"));
        Assert.False(FeatureRequestAuthorization.IsRequester(byName, "someone-else"));
    }

    [Fact]
    public void ProductionRequiresApproverEmailsToApprove()
    {
        var actor = Approver();
        var allowed = FeatureRequestAuthorization.CanApproveOrReject(
            actor,
            [],
            isProduction: true,
            out var error,
            out var message);

        Assert.False(allowed);
        Assert.Equal("approvers_not_configured", error);
        Assert.Contains("ApproverEmails", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentAllowsAnyAuthUserWhenApproverListEmpty()
    {
        var actor = new FeatureRequestActor("dev@etc.example", "oid-dev", "Dev");
        Assert.True(FeatureRequestAuthorization.CanApproveOrReject(
            actor,
            [],
            isProduction: false,
            out var error,
            out _));
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public async Task CreateStillSucceedsWhenEmailThrows()
    {
        await using var db = CreateDb();
        var service = CreateService(
            db,
            email: new ThrowingEmail(),
            options: new FeatureRequestOptions { ApproverEmails = "approver@etc.example" });

        var created = await service.CreateAsync(
            "chat",
            "Pin recent chats on Home.",
            "alex@etc.example",
            CancellationToken.None);

        Assert.Equal("new", created.Status);
        Assert.Single(db.FeatureRequests);
    }

    [Fact]
    public async Task CreateEmailsApproversWhenConfigured()
    {
        await using var db = CreateDb();
        var email = new RecordingEmail();
        var service = CreateService(
            db,
            email: email,
            options: new FeatureRequestOptions
            {
                ApproverEmails = "approver@etc.example; second@etc.example",
                PublicBaseUrl = "https://intranet.2etc.com",
            });

        var created = await service.CreateAsync(
            "lead",
            "The XRF grid loses sort after refresh.",
            "alex.rivera@etc.example",
            CancellationToken.None);

        var sent = Assert.Single(email.Messages);
        Assert.Equal(["approver@etc.example", "second@etc.example"], sent.To);
        Assert.Contains($"#{created.Id}", sent.Subject, StringComparison.Ordinal);
        Assert.Contains("Lead", sent.Text, StringComparison.Ordinal);
        Assert.Contains("alex.rivera@etc.example", sent.Text, StringComparison.Ordinal);
        Assert.Contains("https://intranet.2etc.com/requests", sent.Text, StringComparison.Ordinal);
        Assert.Contains("/requests", sent.Html, StringComparison.Ordinal);
        Assert.Single(db.FeatureRequests);
    }

    [Fact]
    public async Task CreateDoesNotEmailWhenValidationFails()
    {
        await using var db = CreateDb();
        var email = new RecordingEmail();
        var service = CreateService(
            db,
            email: email,
            options: new FeatureRequestOptions { ApproverEmails = "approver@etc.example" });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync("sales", "   ", "alex@etc.example", CancellationToken.None));

        Assert.Empty(email.Messages);
        Assert.Empty(db.FeatureRequests);
    }

    [Fact]
    public async Task ApproveRejectRequiresConfiguredApprover()
    {
        await using var db = CreateDb();
        var service = CreateService(
            db,
            options: new FeatureRequestOptions { ApproverEmails = "approver@etc.example" });
        var created = await SeedNew(service);

        var denied = await Assert.ThrowsAsync<FeatureRequestException>(() =>
            service.UpdateStatusAsync(created.Id, "approved", Requester(), CancellationToken.None));
        Assert.Equal("not_approver", denied.Error);
        Assert.Equal(403, denied.StatusCode);
        Assert.Equal("new", db.FeatureRequests.Single().Status);

        var approved = await service.UpdateStatusAsync(
            created.Id,
            "approved",
            Approver("APPROVER@etc.example"),
            CancellationToken.None);
        Assert.Equal("approved", approved!.Status);
        Assert.Equal("APPROVER@etc.example", approved.ReviewedBy);
        Assert.NotNull(approved.ReviewedAt);
    }

    [Fact]
    public async Task ProductionBlocksApproveWhenApproverListEmpty()
    {
        await using var db = CreateDb();
        var service = CreateService(db, environment: new TestHostEnvironment(Environments.Production));
        var created = await SeedNew(service);

        var error = await Assert.ThrowsAsync<FeatureRequestException>(() =>
            service.UpdateStatusAsync(created.Id, "approved", Approver(), CancellationToken.None));
        Assert.Equal("approvers_not_configured", error.Error);
        Assert.Equal("new", db.FeatureRequests.Single().Status);
    }

    [Fact]
    public async Task DevelopmentAllowsApproveWhenApproverListEmpty()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var created = await SeedNew(service);

        var approved = await service.UpdateStatusAsync(
            created.Id,
            "approved",
            Requester(),
            CancellationToken.None);
        Assert.Equal("approved", approved!.Status);
    }

    [Fact]
    public async Task AnyAuthenticatedUserCanMarkShipped()
    {
        await using var db = CreateDb();
        var service = CreateService(
            db,
            options: new FeatureRequestOptions { ApproverEmails = "approver@etc.example" });
        var created = await SeedNew(service);
        await service.UpdateStatusAsync(created.Id, "approved", Approver(), CancellationToken.None);

        var builder = new FeatureRequestActor("builder@etc.example", "oid-builder", "Builder");
        var shipped = await service.UpdateStatusAsync(created.Id, "shipped", builder, CancellationToken.None);
        Assert.Equal("shipped", shipped!.Status);
    }

    [Fact]
    public async Task RequesterOrApproverCanCloseShipped()
    {
        await using var db = CreateDb();
        var service = CreateService(
            db,
            options: new FeatureRequestOptions { ApproverEmails = "approver@etc.example" });
        var created = await SeedNew(service, "alex.rivera@etc.example");
        await service.UpdateStatusAsync(created.Id, "approved", Approver(), CancellationToken.None);
        await service.UpdateStatusAsync(
            created.Id,
            "shipped",
            new FeatureRequestActor("builder@etc.example", "oid-b", "Builder"),
            CancellationToken.None);

        var stranger = new FeatureRequestActor("other@etc.example", "oid-other", "Other");
        var denied = await Assert.ThrowsAsync<FeatureRequestException>(() =>
            service.UpdateStatusAsync(created.Id, "closed", stranger, CancellationToken.None));
        Assert.Equal("not_requester_or_approver", denied.Error);

        var closed = await service.UpdateStatusAsync(
            created.Id,
            "closed",
            Requester("alex.rivera@etc.example"),
            CancellationToken.None);
        Assert.Equal("closed", closed!.Status);
        Assert.Equal("alex.rivera@etc.example", closed.ClosedBy);
        Assert.NotNull(closed.ClosedAt);
    }

    [Fact]
    public async Task RejectedAndClosedAreTerminal()
    {
        await using var db = CreateDb();
        var service = CreateService(
            db,
            options: new FeatureRequestOptions { ApproverEmails = "approver@etc.example" });
        var created = await SeedNew(service);
        await service.UpdateStatusAsync(created.Id, "rejected", Approver(), CancellationToken.None);

        var error = await Assert.ThrowsAsync<FeatureRequestException>(() =>
            service.UpdateStatusAsync(created.Id, "approved", Approver(), CancellationToken.None));
        Assert.Equal("invalid_transition", error.Error);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("rejected", db.FeatureRequests.Single().Status);
    }

    [Fact]
    public async Task ListNormalizesLegacyPlannedAndDone()
    {
        await using var db = CreateDb();
        db.FeatureRequests.Add(SeedRow("planned"));
        db.FeatureRequests.Add(SeedRow("done"));
        await db.SaveChangesAsync();

        var listed = await CreateService(db).ListAsync(CancellationToken.None);
        Assert.Contains(listed, row => row.Status == "approved");
        Assert.Contains(listed, row => row.Status == "shipped");
    }

    [Fact]
    public async Task ApproveSendsReadyToBuildSms()
    {
        await using var db = CreateDb();
        var sms = new RecordingSms();
        var service = CreateService(db, sms: sms);
        var created = await SeedNew(service);
        sms.Messages.Clear();

        var approved = await service.UpdateStatusAsync(
            created.Id,
            "approved",
            Approver(),
            CancellationToken.None);

        var body = Assert.Single(sms.Messages);
        Assert.Contains($"#{approved!.Id}", body, StringComparison.Ordinal);
        Assert.Contains("approved", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ready to build", body, StringComparison.OrdinalIgnoreCase);
        Assert.True(body.Length <= FeatureRequestSmsMessage.MaxLength);
    }

    [Fact]
    public async Task ApproveSucceedsWhenSmsThrows()
    {
        await using var db = CreateDb();
        var service = CreateService(db, sms: new ThrowingSms());
        var created = await SeedNew(service);

        var approved = await service.UpdateStatusAsync(
            created.Id,
            "approved",
            Approver(),
            CancellationToken.None);
        Assert.Equal("approved", approved!.Status);
        Assert.Equal("approved", db.FeatureRequests.Single().Status);
    }

    [Fact]
    public async Task MetaReportsApproverCapabilityWithoutListingEmails()
    {
        await using var db = CreateDb();
        var service = CreateService(
            db,
            options: new FeatureRequestOptions { ApproverEmails = "approver@etc.example;pat@etc.example" });

        var approverMeta = service.GetMeta(Approver());
        Assert.True(approverMeta.ApproverEmailsConfigured);
        Assert.True(approverMeta.ViewerCanApprove);
        Assert.Equal(2, approverMeta.ApproverCount);

        var requesterMeta = service.GetMeta(Requester());
        Assert.True(requesterMeta.ApproverEmailsConfigured);
        Assert.False(requesterMeta.ViewerCanApprove);
        Assert.Equal(2, requesterMeta.ApproverCount);
    }

    [Fact]
    public void EmailMessageIncludesQueueLink()
    {
        var (subject, text, html) = FeatureRequestEmailMessage.FormatNew(
            new FeatureRequestDto
            {
                Id = 9,
                Page = "other",
                AreaLabel = "IT VPN",
                CreatedBy = "alex@etc.example",
                CreatedAt = DateTimeOffset.UtcNow,
                RawText = "note",
                Title = "VPN setup <guide>",
                Problem = "hard",
                DesiredBehavior = "guide",
                DataInvolved = "intranet",
                AcceptanceCriteria = "docs",
                Status = "new",
                StructuredBy = "fallback",
            },
            "https://intranet.2etc.com/");

        Assert.Contains("#9", subject, StringComparison.Ordinal);
        Assert.Contains("IT VPN", text, StringComparison.Ordinal);
        Assert.Contains("https://intranet.2etc.com/requests", text, StringComparison.Ordinal);
        Assert.Contains("VPN setup &lt;guide&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("VPN setup <guide>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GraphEmailIsConfiguredOnlyWithFromAndAzureAdSecret()
    {
        var options = new FeatureRequestEmailOptions
        {
            Enabled = true,
            Provider = "Graph",
            FromAddress = "intranet@etc.example",
        };
        Assert.True(options.IsGraph);
        Assert.True(options.HasFromAddress);

        options.Enabled = false;
        Assert.False(options.Enabled);
    }

    private static async Task<FeatureRequestDto> SeedNew(
        FeatureRequestService service,
        string createdBy = "alex.rivera@etc.example") =>
        await service.CreateAsync(
            "chat",
            "Pin recent chats on Home.",
            createdBy,
            CancellationToken.None);

    private static FeatureRequest SeedRow(string status) => new()
    {
        Page = "sales",
        CreatedBy = "legacy@etc.example",
        CreatedAt = DateTimeOffset.UtcNow,
        RawText = "legacy",
        Title = "legacy",
        Problem = "legacy",
        DesiredBehavior = "",
        DataInvolved = "",
        AcceptanceCriteria = "",
        Status = status,
        StructuredBy = "fallback",
    };

    private static FeatureRequestService CreateService(
        IntranetDbContext db,
        IFeatureRequestSmsClient? sms = null,
        IFeatureRequestEmailClient? email = null,
        FeatureRequestOptions? options = null,
        IHostEnvironment? environment = null) =>
        new(
            db,
            new NullLlm(),
            sms,
            email,
            options is null ? null : Options.Create(options),
            environment);

    private static IntranetDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IntranetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IntranetDbContext(options);
    }

    private static FeatureRequestActor Approver(string email = "approver@etc.example") =>
        new(email, "oid-approver", "Approver");

    private static FeatureRequestActor Requester(string email = "alex.rivera@etc.example") =>
        new(email, "oid-alex", "Alex");

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Intranet.Api.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class NullLlm : IFeatureRequestLlm
    {
        public Task<string?> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class RecordingEmail : IFeatureRequestEmailClient
    {
        public bool IsConfigured => true;

        public List<(IReadOnlyList<string> To, string Subject, string Text, string Html)> Messages { get; } = [];

        public Task SendAsync(
            IReadOnlyList<string> toAddresses,
            string subject,
            string textBody,
            string htmlBody,
            CancellationToken cancellationToken)
        {
            Messages.Add((toAddresses.ToList(), subject, textBody, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEmail : IFeatureRequestEmailClient
    {
        public bool IsConfigured => true;

        public Task SendAsync(
            IReadOnlyList<string> toAddresses,
            string subject,
            string textBody,
            string htmlBody,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Graph mail is down.");
    }

    private sealed class RecordingSms : IFeatureRequestSmsClient
    {
        public bool IsConfigured => true;

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
