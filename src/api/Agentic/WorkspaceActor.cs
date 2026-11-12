using System.Security.Claims;
using Intranet.Api.KnowledgeBase.Services;

namespace Intranet.Api.Agentic;

public sealed record WorkspaceActor(string Oid, string DisplayName, string? Email)
{
    /// <summary>Local POC identities (dev-you, …). Never used as a Graph /me target.</summary>
    public bool IsDemoIdentity =>
        Oid.StartsWith("dev-", StringComparison.OrdinalIgnoreCase);

    public static WorkspaceActor Demo(AgenticOptions options) =>
        new(options.DemoUserOid, options.DemoUserName, options.DemoUserEmail);

    public static WorkspaceActor FromUser(ClaimsPrincipal user, AgenticOptions options)
    {
        var oid = KnowledgeUser.GetOid(user);
        if (string.IsNullOrWhiteSpace(oid))
        {
            return Demo(options);
        }

        var name = user.FindFirstValue("name")
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? user.Identity?.Name
            ?? options.DemoUserName;
        if (name.Contains('@', StringComparison.Ordinal))
        {
            name = name.Split('@')[0];
        }

        var email = user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue("email")
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? options.DemoUserEmail;
        return new WorkspaceActor(oid, name, email);
    }
}
