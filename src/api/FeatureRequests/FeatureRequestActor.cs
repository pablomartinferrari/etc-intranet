using System.Security.Claims;

namespace Intranet.Api.FeatureRequests;

/// <summary>
/// Signed-in caller identity for feature-request authz. Email prefers typical Entra claims.
/// </summary>
public sealed record FeatureRequestActor(
    string? Email,
    string? ObjectId,
    string? Name,
    bool IsAuthenticated = true)
{
    public static FeatureRequestActor Anonymous { get; } = new(null, null, null, false);

    public string CreatedBy =>
        FirstNonEmpty(Email, ObjectId, Name) ?? "unknown";

    public string Display => CreatedBy;

    public static FeatureRequestActor FromUser(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Anonymous;
        }

        var email = FirstClaim(
            user,
            "preferred_username",
            "email",
            ClaimTypes.Email,
            "upn",
            ClaimTypes.Upn,
            "unique_name");
        if (string.IsNullOrWhiteSpace(email) && user.Identity.Name?.Contains('@', StringComparison.Ordinal) == true)
        {
            email = user.Identity.Name;
        }

        var objectId = FirstClaim(
            user,
            "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier",
            ClaimTypes.NameIdentifier,
            "sub");

        var name = FirstClaim(user, "name", ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(name)
            && !string.IsNullOrWhiteSpace(user.Identity.Name)
            && !user.Identity.Name.Contains('@', StringComparison.Ordinal))
        {
            name = user.Identity.Name;
        }

        return new FeatureRequestActor(
            string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            string.IsNullOrWhiteSpace(objectId) ? null : objectId.Trim(),
            string.IsNullOrWhiteSpace(name) ? null : name.Trim());
    }

    private static string? FirstClaim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
