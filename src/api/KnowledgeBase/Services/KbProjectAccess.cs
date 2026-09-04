namespace Intranet.Api.KnowledgeBase.Services;

public static class KbProjectRoles
{
    public const string Owner = "owner";
    public const string Editor = "editor";
    public const string Viewer = "viewer";

    public const string User = "user";
    public const string Group = "group";

    public static bool IsShareRole(string? role) =>
        string.Equals(role, Editor, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Viewer, StringComparison.OrdinalIgnoreCase);

    public static bool IsPrincipalType(string? type) =>
        string.Equals(type, User, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, Group, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeRole(string role) =>
        string.Equals(role, Editor, StringComparison.OrdinalIgnoreCase) ? Editor : Viewer;

    public static string NormalizePrincipalType(string type) =>
        string.Equals(type, Group, StringComparison.OrdinalIgnoreCase) ? Group : User;
}

public enum KbProjectPermission
{
    View = 0,
    Edit = 1,
    Manage = 2,
}

public sealed record KbProjectAccessResult(
    Guid ProjectId,
    string OwnerOid,
    string Role,
    bool IsShared)
{
    public bool IsOwner => string.Equals(Role, KbProjectRoles.Owner, StringComparison.Ordinal);
    public bool CanManage => IsOwner;
    public bool CanEdit => IsOwner || string.Equals(Role, KbProjectRoles.Editor, StringComparison.Ordinal);
    public bool CanView => true;

    public bool Allows(KbProjectPermission permission) => permission switch
    {
        KbProjectPermission.Manage => CanManage,
        KbProjectPermission.Edit => CanEdit,
        _ => CanView,
    };
}

public readonly record struct KbProjectShareHint(
    string PrincipalType,
    string PrincipalOid,
    string Role);

/// <summary>
/// Pure role resolution for project ownership and Entra user/group shares.
/// </summary>
public static class KbProjectAccess
{
    public static string? ResolveRole(
        string userOid,
        string ownerOid,
        IEnumerable<KbProjectShareHint> shares,
        IReadOnlySet<string> memberGroupOids)
    {
        if (string.IsNullOrWhiteSpace(userOid))
        {
            return null;
        }

        if (OidsEqual(userOid, ownerOid))
        {
            return KbProjectRoles.Owner;
        }

        string? best = null;
        foreach (var share in shares)
        {
            if (!MatchesPrincipal(userOid, share, memberGroupOids))
            {
                continue;
            }

            var role = KbProjectRoles.NormalizeRole(share.Role);
            if (best is null || RoleRank(role) < RoleRank(best))
            {
                best = role;
            }
        }

        return best;
    }

    public static bool MatchesPrincipal(
        string userOid,
        KbProjectShareHint share,
        IReadOnlySet<string> memberGroupOids)
    {
        if (string.Equals(share.PrincipalType, KbProjectRoles.User, StringComparison.OrdinalIgnoreCase))
        {
            return OidsEqual(share.PrincipalOid, userOid);
        }

        if (string.Equals(share.PrincipalType, KbProjectRoles.Group, StringComparison.OrdinalIgnoreCase))
        {
            return memberGroupOids.Contains(share.PrincipalOid)
                || memberGroupOids.Contains(share.PrincipalOid.ToLowerInvariant());
        }

        return false;
    }

    public static bool OidsEqual(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a)
        && !string.IsNullOrWhiteSpace(b)
        && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static int RoleRank(string role) => role switch
    {
        KbProjectRoles.Owner => 0,
        KbProjectRoles.Editor => 1,
        _ => 2,
    };
}

public sealed class KbProjectAccessException : Exception
{
    public int StatusCode { get; }

    public KbProjectAccessException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public static KbProjectAccessException NotFound() =>
        new("Project not found.", StatusCodes.Status404NotFound);

    public static KbProjectAccessException Forbidden(string message = "You do not have permission to do that in this project.") =>
        new(message, StatusCodes.Status403Forbidden);
}
