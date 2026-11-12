using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Data;
using Intranet.Api.Agentic.Repository;
using Intranet.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intranet.Api.Tests;

public class WorkspaceProductionDefaultsTests
{
    [Fact]
    public void AgenticOptionsDefaultToNoDemoSeed()
    {
        var options = new AgenticOptions();
        Assert.False(options.SeedDemoData);
        Assert.False(options.ShareDemoCatalog);
        Assert.False(options.AllowAnonymous);
        Assert.False(options.IncludeDemoDirectory);
        Assert.True(options.UseGraphCalendar);
        Assert.True(options.UseGraphMail);
    }

    [Fact]
    public void CommittedAppSettingsJsonDisablesDemoSeed()
    {
        var path = FindRepoFile(Path.Combine("src", "api", "appsettings.json"));
        var config = new ConfigurationBuilder().AddJsonFile(path).Build();
        var options = new AgenticOptions();
        config.GetSection(AgenticOptions.SectionName).Bind(options);

        Assert.True(options.Enabled);
        Assert.False(options.SeedDemoData);
        Assert.False(options.ShareDemoCatalog);
        Assert.False(options.AllowAnonymous);
        Assert.True(options.UseGraphCalendar);
        Assert.True(options.UseGraphMail);
    }

    [Fact]
    public void DevelopmentAppSettingsKeepLocalDemoSeed()
    {
        var path = FindRepoFile(Path.Combine("src", "api", "appsettings.Development.json"));
        var config = new ConfigurationBuilder().AddJsonFile(path).Build();
        var options = new AgenticOptions();
        config.GetSection(AgenticOptions.SectionName).Bind(options);

        Assert.True(options.SeedDemoData);
        Assert.True(options.ShareDemoCatalog);
        Assert.True(options.AllowAnonymous);
    }

    [Fact]
    public async Task SeedIsNoOpWhenSeedDemoDataIsFalse()
    {
        await using var db = CreateDb();
        var seeder = new WorkspaceSeeder(
            db,
            new LocalRepositorySearch(db),
            Options.Create(new AgenticOptions { Enabled = true, SeedDemoData = false }),
            new StubWebEnvironment(),
            NullLogger<WorkspaceSeeder>.Instance);

        await seeder.SeedAsync(CancellationToken.None);

        Assert.Empty(db.AgenticProjects);
        Assert.Empty(db.AgenticChats);
        Assert.Empty(db.AgenticRepoDocuments);
    }

    [Fact]
    public async Task DemoCleanupRemovesSeededRowsLeavesRealUserDataAndIsIdempotent()
    {
        await using var db = CreateDb();
        var env = new StubWebEnvironment { ContentRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) };
        Directory.CreateDirectory(env.ContentRootPath);
        var options = Options.Create(new AgenticOptions
        {
            Enabled = true,
            SeedDemoData = true,
            RepositoryRoot = "repo",
        });
        var seeder = new WorkspaceSeeder(
            db,
            new LocalRepositorySearch(db),
            options,
            env,
            NullLogger<WorkspaceSeeder>.Instance);
        await seeder.SeedAsync(CancellationToken.None);
        Assert.True(await db.AgenticProjects.AnyAsync(p => p.Id == WorkspaceIds.ProjectAlpha));

        var realProject = new AgenticProject
        {
            Id = Guid.NewGuid(),
            OwnerOid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            Name = "Real bid",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var realChat = new AgenticChat
        {
            Id = Guid.NewGuid(),
            OwnerOid = realProject.OwnerOid,
            Title = "Real chat",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.AgenticProjects.Add(realProject);
        db.AgenticChats.Add(realChat);
        await db.SaveChangesAsync();

        var cleanup = new WorkspaceDemoCleanup(
            db,
            options,
            env,
            NullLogger<WorkspaceDemoCleanup>.Instance);
        var first = await cleanup.RunAsync();
        var second = await cleanup.RunAsync();

        Assert.True(first > 0);
        Assert.Equal(0, second);
        Assert.False(await db.AgenticProjects.AnyAsync(p => p.Id == WorkspaceIds.ProjectAlpha));
        Assert.False(await db.AgenticChats.AnyAsync(c => c.Id == WorkspaceIds.ChatPlaceholder));
        Assert.False(await db.AgenticRepoDocuments.AnyAsync(d => d.Id == WorkspaceIds.DocQuarterly1));
        Assert.True(await db.AgenticProjects.AnyAsync(p => p.Id == realProject.Id));
        Assert.True(await db.AgenticChats.AnyAsync(c => c.Id == realChat.Id));
        Assert.True(await db.AgenticRepoFolders.AnyAsync(f => f.ParentId == null));
    }

    [Fact]
    public void DirectoryOmitsDemoPrincipalsWhenShareDemoCatalogIsOff()
    {
        var directory = new WorkspaceDirectory(
            new StubGraphDirectory(),
            new StubTokens(),
            Options.Create(new AgenticOptions { ShareDemoCatalog = false, AllowAnonymous = false }),
            NullLogger<WorkspaceDirectory>.Instance);

        Assert.Empty(directory.LocalPrincipals);
        Assert.False(directory.GraphConfigured);
    }

    [Fact]
    public async Task DirectoryKeepsDemoPrincipalsOnlyWhenEnabled()
    {
        var directory = new WorkspaceDirectory(
            new StubGraphDirectory(),
            new StubTokens(),
            Options.Create(new AgenticOptions { ShareDemoCatalog = true, AllowAnonymous = true }),
            NullLogger<WorkspaceDirectory>.Instance);

        var results = await directory.SearchAsync("User B", CancellationToken.None);
        Assert.Contains(results, p => p.Oid == WorkspaceIds.DemoUserBOid);
    }

    [Fact]
    public void RepositoryStoragePrefersHomeOnAppService()
    {
        var previous = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", Path.Combine(Path.GetTempPath(), "azure-home"));
            var root = RepositoryStorage.ResolveRoot(
                new AgenticOptions { RepositoryRoot = "data/agentic-repo" },
                new StubWebEnvironment { ContentRootPath = "/readonly/wwwroot" });
            Assert.Contains("azure-home", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("wwwroot", root, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", previous);
        }
    }

    private static IntranetDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IntranetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IntranetDbContext(options);
    }

    private static string FindRepoFile(string relativeFromRepoRoot)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "api", "Intranet.Api.csproj")))
            {
                return Path.Combine(dir.FullName, relativeFromRepoRoot);
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repo file {relativeFromRepoRoot}.");
    }

    private sealed class StubWebEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StubTokens : Intranet.Api.Agentic.Day.IUserGraphTokenProvider
    {
        public bool IsConfigured => false;

        public Task<string> AcquireCalendarTokenAsync(CancellationToken cancellationToken) => Task.FromResult("");

        public Task<string> AcquireMailTokenAsync(CancellationToken cancellationToken) => Task.FromResult("");

        public Task<string> AcquireDirectoryUserTokenAsync(CancellationToken cancellationToken) => Task.FromResult("");

        public Task<string> AcquireDirectoryGroupTokenAsync(CancellationToken cancellationToken) => Task.FromResult("");
    }

    private sealed class StubGraphDirectory : Intranet.Api.KnowledgeBase.Services.IGraphDirectoryClient
    {
        public bool IsConfigured => false;

        public Task<IReadOnlyList<Intranet.Api.KnowledgeBase.Models.DirectoryPrincipalDto>> SearchAsync(
            string query,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Intranet.Api.KnowledgeBase.Models.DirectoryPrincipalDto>>([]);

        public Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
            string userOid,
            IReadOnlyCollection<string> groupOids,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }
}
