using Intranet.Api.Agentic;
using Intranet.Api.Agentic.Data;
using Xunit;

namespace Intranet.Api.Tests;

public class AgenticAccessTests
{
    private const string Owner = "owner-oid";
    private const string Editor = "editor-oid";
    private const string Viewer = "viewer-oid";
    private const string Outsider = "outsider-oid";
    private const string GroupId = "group-ops";

    [Fact]
    public void OwnerWinsOverShare()
    {
        var role = AgenticAccess.ResolveRole(
            Owner,
            Owner,
            [new AgenticShareHint("user", Owner, "editor")],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(AgenticRoles.Owner, role);
    }

    [Fact]
    public void DirectUserShareGrantsEditorOrViewer()
    {
        var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(AgenticRoles.Editor, AgenticAccess.ResolveRole(
            Editor, Owner, [new AgenticShareHint("user", Editor, "editor")], none));
        Assert.Equal(AgenticRoles.Viewer, AgenticAccess.ResolveRole(
            Viewer, Owner, [new AgenticShareHint("user", Viewer, "viewer")], none));
        Assert.Null(AgenticAccess.ResolveRole(
            Outsider, Owner, [new AgenticShareHint("user", Editor, "editor")], none));
    }

    [Fact]
    public void GroupShareGrantsAccessWhenMember()
    {
        var shares = new[] { new AgenticShareHint("group", GroupId, "viewer") };
        var member = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { GroupId };
        var notMember = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(AgenticRoles.Viewer, AgenticAccess.ResolveRole(Viewer, Owner, shares, member));
        Assert.Null(AgenticAccess.ResolveRole(Viewer, Owner, shares, notMember));
    }

    [Fact]
    public void ChatRoleInheritsProjectShare()
    {
        var project = new AgenticProject
        {
            OwnerOid = Owner,
            Shares =
            [
                new AgenticProjectShare
                {
                    PrincipalType = "group",
                    PrincipalOid = GroupId,
                    Role = "editor",
                },
            ],
        };
        var chat = new AgenticChat
        {
            OwnerOid = Owner,
            Project = project,
            Shares = [],
        };
        var member = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { GroupId };

        Assert.Equal(AgenticRoles.Editor, AgenticAccess.ResolveChatRole(Viewer, chat, member));
    }

    [Fact]
    public void MostPrivilegedShareWins()
    {
        var shares = new[]
        {
            new AgenticShareHint("user", Viewer, "viewer"),
            new AgenticShareHint("group", GroupId, "editor"),
        };
        var member = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { GroupId };
        Assert.Equal(AgenticRoles.Editor, AgenticAccess.ResolveRole(Viewer, Owner, shares, member));
    }
}
