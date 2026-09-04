using System.Security.Claims;

namespace Intranet.Api.KnowledgeBase.Services;

public static class KnowledgeUser
{
    public static string? GetOid(ClaimsPrincipal user) =>
        user.FindFirst("oid")?.Value
        ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
}
