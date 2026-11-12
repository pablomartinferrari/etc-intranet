namespace Intranet.Api.MultifamilyLbp.Options;

public sealed class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}

public sealed class SharePointOptions
{
    public const string SectionName = "SharePoint";

    /// <summary>Full site URL, e.g. https://tenant.sharepoint.com/sites/YourSite</summary>
    public string SiteUrl { get; set; } = "";

    public string SourceLibraryName { get; set; } = "XRF-SourceFiles";
}
