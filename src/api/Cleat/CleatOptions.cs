namespace Intranet.Api.Cleat;

public sealed class CleatOptions
{
    public const string SectionName = "Cleat";

    /// <summary>CLEATUS REST API origin. Default matches the live OpenAPI servers[0].</summary>
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

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
