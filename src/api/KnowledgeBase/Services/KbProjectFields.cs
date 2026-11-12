namespace Intranet.Api.KnowledgeBase.Services;

public static class KbProjectFields
{
    public const int AreaMaxLength = 80;

    public static string? NormalizeArea(string? area)
    {
        if (string.IsNullOrWhiteSpace(area))
        {
            return null;
        }

        var trimmed = area.Trim();
        return trimmed.Length <= AreaMaxLength
            ? trimmed
            : trimmed[..AreaMaxLength];
    }
}
