using Intranet.Api.Agentic.Data;

namespace Intranet.Api.Agentic;

public static class AgenticRoles
{
    public const string Owner = "owner";
    public const string Editor = "editor";
    public const string Viewer = "viewer";
    public const string User = "user";
    public const string Group = "group";

    public static bool CanView(string? role) =>
        role is Owner or Editor or Viewer;

    public static bool CanEdit(string? role) =>
        role is Owner or Editor;

    public static bool CanManage(string? role) =>
        role is Owner;

    public static int Rank(string? role) => role switch
    {
        Owner => 3,
        Editor => 2,
        Viewer => 1,
        _ => 0,
    };

    public static string? Best(params string?[] roles)
    {
        string? best = null;
        foreach (var role in roles)
        {
            if (Rank(role) > Rank(best))
            {
                best = role;
            }
        }

        return best;
    }
}

public readonly record struct AgenticShareHint(string PrincipalType, string PrincipalOid, string Role);

/// <summary>
/// Same resolution rules as <c>KbProjectAccess</c>: owner wins, then the most
/// privileged matching user or group share.
/// </summary>
public static class AgenticAccess
{
    public static bool OidsEqual(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public static string? ResolveRole(
        string userOid,
        string ownerOid,
        IEnumerable<AgenticShareHint> shares,
        IReadOnlySet<string> memberGroupOids)
    {
        if (OidsEqual(userOid, ownerOid))
        {
            return AgenticRoles.Owner;
        }

        string? best = null;
        foreach (var share in shares)
        {
            var matches = share.PrincipalType.Equals(AgenticRoles.Group, StringComparison.OrdinalIgnoreCase)
                ? memberGroupOids.Contains(share.PrincipalOid)
                : OidsEqual(share.PrincipalOid, userOid);
            if (!matches)
            {
                continue;
            }

            best = AgenticRoles.Best(best, NormalizeRole(share.Role));
        }

        return best;
    }

    public static string? ResolveChatRole(
        string userOid,
        AgenticChat chat,
        IReadOnlySet<string> memberGroupOids)
    {
        var shares = chat.Shares.Select(s => new AgenticShareHint(s.PrincipalType, s.PrincipalOid, s.Role));
        var direct = ResolveRole(userOid, chat.OwnerOid, shares, memberGroupOids);
        if (chat.Project is null)
        {
            return direct;
        }

        var projectShares = chat.Project.Shares.Select(s =>
            new AgenticShareHint(s.PrincipalType, s.PrincipalOid, s.Role));
        var viaProject = ResolveRole(userOid, chat.Project.OwnerOid, projectShares, memberGroupOids);
        return AgenticRoles.Best(direct, viaProject);
    }

    public static string? ResolveProjectRole(
        string userOid,
        AgenticProject project,
        IReadOnlySet<string> memberGroupOids)
    {
        var shares = project.Shares.Select(s => new AgenticShareHint(s.PrincipalType, s.PrincipalOid, s.Role));
        return ResolveRole(userOid, project.OwnerOid, shares, memberGroupOids);
    }

    public static string NormalizeRole(string? role)
    {
        var raw = role?.Trim().ToLowerInvariant();
        return raw is AgenticRoles.Editor or AgenticRoles.Viewer ? raw : AgenticRoles.Viewer;
    }

    public static string NormalizePrincipalType(string? type)
    {
        var raw = type?.Trim().ToLowerInvariant();
        return raw is AgenticRoles.Group ? AgenticRoles.Group : AgenticRoles.User;
    }
}
