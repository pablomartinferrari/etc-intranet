namespace Intranet.Api.Agentic;

public sealed class AgenticOptions
{
    public const string SectionName = "Features:AgenticWorkspace";

    /// <summary>Master switch. When false, /api/workspace returns 404 and the SPA should hide the route.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Development convenience: unauthenticated /api/workspace and /hubs/workspace
    /// calls run as the demo user so the POC works locally without Entra.
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>Seed wireframe-shaped projects, chats, and repository docs on startup.</summary>
    public bool SeedDemoData { get; set; } = true;

    /// <summary>
    /// In Development, treat signed-in users as owners of the seeded catalog so
    /// Entra login still shows the POC sample workspace.
    /// </summary>
    public bool ShareDemoCatalog { get; set; }

    /// <summary>When true and Graph is configured, overlay the signed-in user's calendar on Your Day.</summary>
    public bool UseGraphCalendar { get; set; }

    /// <summary>Disk root for uploaded repository files. Relative paths are under ContentRoot.</summary>
    public string RepositoryRoot { get; set; } = "App_Data/agentic-repo";

    /// <summary>Search backend. "local" is the POC inverted index; "azure" is the future seam.</summary>
    public string SearchProvider { get; set; } = "local";

    /// <summary>Reserved for the Azure AI Search seam. Not required for the local POC.</summary>
    public string? AzureSearchEndpoint { get; set; }

    /// <summary>Reserved for the Azure AI Search seam. Do not put secrets in appsettings committed to git.</summary>
    public string? AzureSearchApiKey { get; set; }

    public string DemoUserOid { get; set; } = WorkspaceIds.DemoYouOid;

    public string DemoUserName { get; set; } = "You";

    public string DemoUserEmail { get; set; } = "you@etc.local";
}
