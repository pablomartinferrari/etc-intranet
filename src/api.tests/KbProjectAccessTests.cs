using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Data.Entities;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intranet.Api.Tests;

public class KbProjectAccessTests
{
    private const string Owner = "owner-oid";
    private const string Editor = "editor-oid";
    private const string Viewer = "viewer-oid";
    private const string Outsider = "outsider-oid";
    private const string GroupId = "group-finance";

    [Fact]
    public void OwnerWinsOverShare()
    {
        var role = KbProjectAccess.ResolveRole(
            Owner,
            Owner,
            [new KbProjectShareHint("user", Owner, "editor")],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(KbProjectRoles.Owner, role);
    }

    [Fact]
    public void DirectUserShareGrantsEditorOrViewer()
    {
        var shares = new[] { new KbProjectShareHint("user", Editor, "editor") };
        var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(KbProjectRoles.Editor, KbProjectAccess.ResolveRole(Editor, Owner, shares, none));
        Assert.Equal(KbProjectRoles.Viewer, KbProjectAccess.ResolveRole(
            Viewer,
            Owner,
            [new KbProjectShareHint("user", Viewer, "viewer")],
            none));
        Assert.Null(KbProjectAccess.ResolveRole(Outsider, Owner, shares, none));
    }

    [Fact]
    public void GroupShareGrantsAccessWhenMember()
    {
        var shares = new[] { new KbProjectShareHint("group", GroupId, "viewer") };
        var member = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { GroupId };
        var notMember = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(KbProjectRoles.Viewer, KbProjectAccess.ResolveRole(Viewer, Owner, shares, member));
        Assert.Null(KbProjectAccess.ResolveRole(Viewer, Owner, shares, notMember));
    }

    [Fact]
    public void MostPrivilegedShareWins()
    {
        var shares = new[]
        {
            new KbProjectShareHint("user", Viewer, "viewer"),
            new KbProjectShareHint("group", GroupId, "editor"),
        };
        var member = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { GroupId };

        Assert.Equal(KbProjectRoles.Editor, KbProjectAccess.ResolveRole(Viewer, Owner, shares, member));
    }

    [Fact]
    public void OidCompareIsCaseInsensitive()
    {
        var shares = new[] { new KbProjectShareHint("user", "AABBCC", "editor") };
        var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(KbProjectRoles.Editor, KbProjectAccess.ResolveRole("aabbcc", Owner, shares, none));
        Assert.True(KbProjectAccess.OidsEqual("Aa", "aa"));
    }

    [Fact]
    public async Task ServiceReturnsOwnerEditorViewerAndGroup()
    {
        await using var db = CreateDb();
        var project = AddProject(db, Owner, "Bid desk");
        db.ProjectShares.AddRange(
            Share(project.Id, "user", Editor, "editor", "Alex Editor"),
            Share(project.Id, "user", Viewer, "viewer", "Val Viewer"));
        await db.SaveChangesAsync();

        var graph = new FakeGraph { MemberGroups = { GroupId } };
        var service = new KbProjectAccessService(db, graph, NullLogger<KbProjectAccessService>.Instance);

        var owner = await service.GetAccessAsync(project.Id, Owner, CancellationToken.None);
        var editor = await service.GetAccessAsync(project.Id, Editor, CancellationToken.None);
        var viewer = await service.GetAccessAsync(project.Id, Viewer, CancellationToken.None);
        var outsider = await service.GetAccessAsync(project.Id, Outsider, CancellationToken.None);

        Assert.Equal(KbProjectRoles.Owner, owner?.Role);
        Assert.True(owner?.IsShared);
        Assert.True(owner?.CanManage);
        Assert.Equal(KbProjectRoles.Editor, editor?.Role);
        Assert.True(editor?.CanEdit);
        Assert.False(editor?.CanManage);
        Assert.Equal(KbProjectRoles.Viewer, viewer?.Role);
        Assert.False(viewer?.CanEdit);
        Assert.Null(outsider);

        db.ProjectShares.Add(Share(project.Id, "group", GroupId, "editor", "Finance"));
        await db.SaveChangesAsync();
        var groupMember = await service.GetAccessAsync(project.Id, "group-user", CancellationToken.None);
        Assert.Equal(KbProjectRoles.Editor, groupMember?.Role);
    }

    [Fact]
    public async Task ListIncludesOwnedUserShareAndGroupShare()
    {
        await using var db = CreateDb();
        var mine = AddProject(db, Owner, "Mine");
        var sharedUser = AddProject(db, "someone-else", "Shared user");
        var sharedGroup = AddProject(db, "someone-else", "Shared group");
        var hidden = AddProject(db, "someone-else", "Hidden");
        db.ProjectShares.Add(Share(sharedUser.Id, "user", Owner, "viewer", "Me"));
        db.ProjectShares.Add(Share(sharedGroup.Id, "group", GroupId, "viewer", "Finance"));
        db.ProjectShares.Add(Share(hidden.Id, "group", "other-group", "viewer", "Other"));
        await db.SaveChangesAsync();

        var graph = new FakeGraph { MemberGroups = { GroupId } };
        var service = new KbProjectAccessService(db, graph, NullLogger<KbProjectAccessService>.Instance);
        var list = await service.ListAccessibleAsync(Owner, CancellationToken.None);

        Assert.Equal(3, list.Count);
        Assert.Contains(list, p => p.Project.Id == mine.Id && p.Access.IsOwner);
        Assert.Contains(list, p => p.Project.Id == sharedUser.Id && p.Access.Role == KbProjectRoles.Viewer);
        Assert.Contains(list, p => p.Project.Id == sharedGroup.Id && p.Access.Role == KbProjectRoles.Viewer);
        Assert.DoesNotContain(list, p => p.Project.Id == hidden.Id);
    }

    [Fact]
    public async Task RequireManageForbidsEditor()
    {
        await using var db = CreateDb();
        var project = AddProject(db, Owner, "Locked");
        db.ProjectShares.Add(Share(project.Id, "user", Editor, "editor", "Alex"));
        await db.SaveChangesAsync();

        var service = new KbProjectAccessService(db, new FakeGraph(), NullLogger<KbProjectAccessService>.Instance);
        await service.RequireAsync(project.Id, Editor, KbProjectPermission.Edit, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<KbProjectAccessException>(() =>
            service.RequireAsync(project.Id, Editor, KbProjectPermission.Manage, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    private static KnowledgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new KnowledgeDbContext(options);
    }

    private static KbProject AddProject(KnowledgeDbContext db, string ownerOid, string name)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new KbProject
        {
            Id = Guid.NewGuid(),
            UserOid = ownerOid,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Projects.Add(project);
        return project;
    }

    private static KbProjectShare Share(
        Guid projectId,
        string type,
        string oid,
        string role,
        string displayName) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PrincipalType = type,
            PrincipalOid = oid,
            PrincipalDisplayName = displayName,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByOid = Owner,
        };

    private sealed class FakeGraph : IGraphDirectoryClient
    {
        public bool IsConfigured { get; set; } = true;
        public HashSet<string> MemberGroups { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<DirectoryPrincipalDto>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DirectoryPrincipalDto>>([]);

        public Task<IReadOnlySet<string>> CheckMemberGroupsAsync(
            string userOid,
            IReadOnlyCollection<string> groupOids,
            CancellationToken cancellationToken)
        {
            var matched = groupOids
                .Where(id => MemberGroups.Contains(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlySet<string>>(matched);
        }
    }
}
