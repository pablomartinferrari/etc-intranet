namespace Intranet.Api.Cleat;

public sealed class CleatOptions
{
    public const string SectionName = "Cleat";

    /// <summary>
    /// CLEATUS REST API origin. OpenAPI lists <c>https://api.cleat.ai</c> with paths
    /// <c>/v1/...</c>, but the live host serves those routes under <c>/api/v1/...</c>.
    /// <see cref="ResolvedBaseUrl"/> appends <c>/api</c> when the origin is the default host.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.cleat.ai";

    /// <summary>
    /// CLEATUS web app origin used to build "Open in CLEATUS" links.
    /// Public OpenAPI response schemas do not document a permalink field.
    /// </summary>
    public string AppBaseUrl { get; set; } = "https://www.cleat.ai";

    /// <summary>
    /// Long-lived key sent as the X-Api-Key header.
    /// Bind from Cleat__ApiKey (env), user secrets, App Settings, or Key Vault.
    /// Never commit a real value.
    /// </summary>
    public string? ApiKey { get; set; }

    public string? TrimmedApiKey => string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim();

    public bool HasApiKey => TrimmedApiKey is not null;

    /// <summary>
    /// HttpClient base address. Default origin <c>https://api.cleat.ai</c> is mapped to
    /// <c>https://api.cleat.ai/api</c> so relative <c>v1/...</c> paths hit the live API.
    /// </summary>
    public string ResolvedBaseUrl
    {
        get
        {
            var origin = string.IsNullOrWhiteSpace(BaseUrl)
                ? "https://api.cleat.ai"
                : BaseUrl.Trim().TrimEnd('/');
            if (origin.Equals("https://api.cleat.ai", StringComparison.OrdinalIgnoreCase)
                || origin.Equals("http://api.cleat.ai", StringComparison.OrdinalIgnoreCase))
            {
                origin += "/api";
            }

            return origin;
        }
    }
}
