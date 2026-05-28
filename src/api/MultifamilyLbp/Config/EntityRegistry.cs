namespace Intranet.Api.MultifamilyLbp.Config;

public sealed record EntityDefinition(string Slug, string DisplayName, string Description);

public static class EntityRegistry
{
    public const string MultifamilyLbp = "multifamily-lbp";

    private static readonly EntityDefinition[] All =
    [
        new(MultifamilyLbp, "Multifamily LBP", "XRF lead-based paint inspection for multifamily housing"),
    ];

    public static bool IsValid(string slug) =>
        All.Any(e => string.Equals(e.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static EntityDefinition? Get(string slug) =>
        All.FirstOrDefault(e => string.Equals(e.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<EntityDefinition> List() => All;
}
