namespace Intranet.Api.Agentic.Repository;

/// <summary>
/// Resolves where uploaded workspace files live.
/// Azure App Service (Linux in <c>infra/modules/app-service.bicep</c>) sets
/// <c>WEBSITE_RUN_FROM_PACKAGE=1</c>, so ContentRoot is a read-only zip.
/// Persistent disk is <c>/home</c> on Linux and <c>%HOME%</c> on Windows
/// (both exposed as the <c>HOME</c> environment variable).
/// </summary>
public static class RepositoryStorage
{
    public const string DefaultRelativeRoot = "data/agentic-repo";

    public static string ResolveRoot(AgenticOptions options, IWebHostEnvironment environment)
    {
        var configured = string.IsNullOrWhiteSpace(options.RepositoryRoot)
            ? DefaultRelativeRoot
            : options.RepositoryRoot.Trim();
        configured = configured.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            return Path.GetFullPath(Path.Combine(home, configured));
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured));
    }
}
