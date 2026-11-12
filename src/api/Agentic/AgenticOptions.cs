namespace Intranet.Api.Agentic;

public sealed class AgenticOptions
{
    public const string SectionName = "Features:AgenticWorkspace";

    /// <summary>Master switch. When false, /api/workspace returns 404 and the SPA should hide the route.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Development convenience: unauthenticated /api/workspace and /hubs/workspace
    /// calls run as the demo user so local review works without Entra.
    /// Must stay false in Production.
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// Seed the wireframe catalog on startup. Default false — Production must not
    /// ship demo projects/chats. Enable only in <c>appsettings.Development.json</c>.
    /// </summary>
    public bool SeedDemoData { get; set; }

    /// <summary>
    /// In Development, treat signed-in users as owners of the seeded catalog so
    /// Entra login still shows the sample workspace. Must stay false in Production.
    /// </summary>
    public bool ShareDemoCatalog { get; set; }

    /// <summary>
    /// When true (the default) and Entra is configured with a confidential client,
    /// Your Day reads the signed-in user's Outlook calendar via delegated Graph
    /// <c>GET /me/calendarView</c>. Production never falls back to mock meetings.
    /// </summary>
    public bool UseGraphCalendar { get; set; } = true;

    /// <summary>
    /// When true (the default) and Entra is configured with a confidential client,
    /// Your Day follow-ups read the signed-in user's Outlook mail via delegated
    /// Graph <c>Mail.Read</c> (separate OBO token from calendar). Production never
    /// mocks follow-ups.
    /// </summary>
    public bool UseGraphMail { get; set; } = true;

    /// <summary>
    /// Disk root for uploaded repository files. Relative paths resolve under
    /// <c>HOME</c> on Azure App Service (persistent across deploys) or ContentRoot locally.
    /// </summary>
    public string RepositoryRoot { get; set; } = "data/agentic-repo";

    /// <summary>Search backend. "local" is the inverted index; "azure" is the future seam.</summary>
    public string SearchProvider { get; set; } = "local";

    /// <summary>Reserved for the Azure AI Search seam. Not required for local search.</summary>
    public string? AzureSearchEndpoint { get; set; }

    /// <summary>Reserved for the Azure AI Search seam. Do not put secrets in appsettings committed to git.</summary>
    public string? AzureSearchApiKey { get; set; }

    public string DemoUserOid { get; set; } = WorkspaceIds.DemoYouOid;

    public string DemoUserName { get; set; } = "You";

    public string DemoUserEmail { get; set; } = "you@etc.local";

    /// <summary>Local demo principals (User B/C/D, Group X) for Share. Off unless Development seeding is on.</summary>
    public bool IncludeDemoDirectory => AllowAnonymous || ShareDemoCatalog;
}
