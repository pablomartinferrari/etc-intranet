using Intranet.Api.KnowledgeBase.AgentSources;
using Intranet.Api.KnowledgeBase.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class AgentSourceLimitsTests
{
    private static AgentSourceOptions Defaults => new();

    [Fact]
    public void SoftFolderAutoRuns()
    {
        var decision = AgentSourceLimitEvaluator.Evaluate(10, 1_000_000, Defaults);
        Assert.Equal(AgentSourceLimitTier.Soft, decision.Tier);
        Assert.True(decision.CanAutoRun);
        Assert.False(decision.RequiresConfirm);
        Assert.False(decision.RequiresApproval);
    }

    [Fact]
    public void SoftBoundaryStillAutoRuns()
    {
        var options = Defaults;
        var decision = AgentSourceLimitEvaluator.Evaluate(options.SoftMaxFiles, options.SoftMaxBytes, options);
        Assert.Equal(AgentSourceLimitTier.Soft, decision.Tier);
        Assert.True(decision.CanAutoRun);
    }

    [Fact]
    public void OneFileOverSoftRequiresConfirm()
    {
        var options = Defaults;
        var decision = AgentSourceLimitEvaluator.Evaluate(options.SoftMaxFiles + 1, 1, options);
        Assert.Equal(AgentSourceLimitTier.Medium, decision.Tier);
        Assert.False(decision.CanAutoRun);
        Assert.True(decision.RequiresConfirm);
        Assert.False(decision.RequiresApproval);
    }

    [Fact]
    public void BytesOverSoftRequiresConfirm()
    {
        var options = Defaults;
        var decision = AgentSourceLimitEvaluator.Evaluate(1, options.SoftMaxBytes + 1, options);
        Assert.Equal(AgentSourceLimitTier.Medium, decision.Tier);
        Assert.True(decision.RequiresConfirm);
    }

    [Fact]
    public void MediumBoundaryStillConfirms()
    {
        var options = Defaults;
        var decision = AgentSourceLimitEvaluator.Evaluate(options.MediumMaxFiles, options.MediumMaxBytes, options);
        Assert.Equal(AgentSourceLimitTier.Medium, decision.Tier);
        Assert.True(decision.RequiresConfirm);
        Assert.False(decision.RequiresApproval);
    }

    [Fact]
    public void OverMediumRequiresApproval()
    {
        var options = Defaults;
        var decision = AgentSourceLimitEvaluator.Evaluate(options.MediumMaxFiles + 1, 1, options);
        Assert.Equal(AgentSourceLimitTier.Hard, decision.Tier);
        Assert.True(decision.RequiresApproval);
        Assert.False(decision.CanAutoRun);
        Assert.False(decision.RequiresConfirm);
        Assert.Contains("admin", decision.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HugeBytesRequireApproval()
    {
        var decision = AgentSourceLimitEvaluator.Evaluate(1, 2L * 1024 * 1024 * 1024 * 1024, Defaults);
        Assert.Equal(AgentSourceLimitTier.Hard, decision.Tier);
        Assert.True(decision.RequiresApproval);
    }

    [Fact]
    public void RejectsNegativeCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgentSourceLimitEvaluator.Evaluate(-1, 0, Defaults));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgentSourceLimitEvaluator.Evaluate(0, -1, Defaults));
    }

    [Fact]
    public void FormatsBytes()
    {
        Assert.Equal("512 B", AgentSourceLimitEvaluator.FormatBytes(512));
        Assert.Equal("1 KB", AgentSourceLimitEvaluator.FormatBytes(1024));
        Assert.Equal("2 GB", AgentSourceLimitEvaluator.FormatBytes(2L * 1024 * 1024 * 1024));
    }
}

public class AgentSourceJobStateTests
{
    [Theory]
    [InlineData("queued", "probing")]
    [InlineData("queued", "running")]
    [InlineData("queued", "failed")]
    [InlineData("queued", "awaiting_approval")]
    [InlineData("probing", "running")]
    [InlineData("probing", "failed")]
    [InlineData("running", "done")]
    [InlineData("running", "failed")]
    [InlineData("awaiting_approval", "queued")]
    [InlineData("awaiting_approval", "failed")]
    public void AllowsForwardTransitions(string from, string to)
    {
        Assert.Equal(to, AgentSourceJobStateMachine.Transition(from, to));
    }

    [Theory]
    [InlineData("done", "queued")]
    [InlineData("done", "running")]
    [InlineData("failed", "queued")]
    [InlineData("running", "queued")]
    [InlineData("awaiting_approval", "running")]
    [InlineData("probing", "done")]
    public void RejectsIllegalTransitions(string from, string to)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            AgentSourceJobStateMachine.Transition(from, to));
        Assert.Contains(from, error.Message, StringComparison.Ordinal);
        Assert.Contains(to, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySetsTimestampsAndError()
    {
        var job = new Intranet.Api.Data.Entities.AgentSourceJob
        {
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        AgentSourceJobStateMachine.Apply(job, "running");
        Assert.Equal("running", job.Status);
        Assert.NotNull(job.StartedAt);
        Assert.Null(job.FinishedAt);

        AgentSourceJobStateMachine.Apply(job, "failed", "Graph 403");
        Assert.Equal("failed", job.Status);
        Assert.Equal("Graph 403", job.ErrorMessage);
        Assert.NotNull(job.FinishedAt);
    }
}

public class AgentSourceValidationTests
{
    [Fact]
    public void ProbeRequiresSiteUrl()
    {
        var error = AgentSourceRequestValidator.ValidateProbe("  ", null);
        Assert.Contains("site URL", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProbeRejectsNonUrl()
    {
        var error = AgentSourceRequestValidator.ValidateProbe("not a url", "Policies");
        Assert.Contains("http", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProbeRejectsSharingLinks()
    {
        var error = AgentSourceRequestValidator.ValidateProbe(
            "https://contoso.sharepoint.com/:f:/s/HR/abc123",
            null);
        Assert.Contains("Sharing links", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsesSiteAndFolder()
    {
        Assert.True(SharePointFolderUrlParser.TryParse(
            "https://contoso.sharepoint.com/sites/HR",
            "Shared Documents/Policies",
            out var folder,
            out var error));
        Assert.Null(error);
        Assert.Equal("contoso.sharepoint.com:/sites/HR", folder!.SiteKey);
        Assert.Equal("Shared Documents/Policies", folder.FolderPath);
        Assert.Contains("Policies", SharePointFolderUrlParser.FolderIdentity(folder), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsesFolderFromAllItemsQuery()
    {
        Assert.True(SharePointFolderUrlParser.TryParse(
            "https://contoso.sharepoint.com/sites/HR/Shared%20Documents/Forms/AllItems.aspx?id=%2Fsites%2FHR%2FShared%20Documents%2FPolicies",
            null,
            out var folder,
            out _));
        Assert.Equal("Shared Documents/Policies", folder!.FolderPath);
    }

    [Fact]
    public void ConnectRejectsLongLabel()
    {
        var error = AgentSourceRequestValidator.ValidateConnect(
            "https://contoso.sharepoint.com/sites/HR",
            "Docs",
            new string('x', 201));
        Assert.Contains("label", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("policy.pdf", 100, true)]
    [InlineData("notes.docx", 100, true)]
    [InlineData("slide.pptx", 100, true)]
    [InlineData("sheet.xlsx", 100, true)]
    [InlineData("readme.md", 100, true)]
    [InlineData("video.mp4", 100, false)]
    [InlineData("image.iso", 100, false)]
    [InlineData("setup.exe", 100, false)]
    [InlineData("legacy.doc", 100, false)]
    [InlineData("ok.pdf", 60L * 1024 * 1024, false)]
    public void FileRulesMatchJunkAndSize(string name, long size, bool allowed)
    {
        Assert.Equal(allowed, AgentSourceFileRules.ShouldIngest(name, size, new AgentSourceOptions()));
    }

    [Fact]
    public void EmbeddingUrlUsesOpenAiV1()
    {
        var url = OpenAiCompatibleEmbeddingClient.ResolveEmbeddingsUrl(
            "https://api.openai.com/v1",
            "text-embedding-3-small",
            "2024-10-21",
            azure: false);
        Assert.Equal("https://api.openai.com/v1/embeddings", url);
    }

    [Fact]
    public void EmbeddingUrlUsesAzureDeployment()
    {
        var url = OpenAiCompatibleEmbeddingClient.ResolveEmbeddingsUrl(
            "https://etc-openai.openai.azure.com",
            "intranet-embed",
            "2024-10-21",
            azure: true);
        Assert.Equal(
            "https://etc-openai.openai.azure.com/openai/deployments/intranet-embed/embeddings?api-version=2024-10-21",
            url);
    }
}
