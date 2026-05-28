using System.Collections.Frozen;

namespace Intranet.Api.MultifamilyLbp.Services.Excel;

/// <summary>Port of SPFx <c>ExcelColumnMapping.ts</c> — header synonyms and matching rules.</summary>
public sealed class ExcelColumnMapping
{
    /// <summary>Headers that must never map to unit number (e.g. lab "Units" = mg/cm², not dwelling unit).</summary>
    public static readonly FrozenSet<string> UnitNumberMatchExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "units",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public string[] ReadingId { get; init; } = [];
    public string[] Component { get; init; } = [];
    public string[] Color { get; init; } = [];
    public string[] LeadContent { get; init; } = [];
    public string[] Location { get; init; } = [];
    public string[] UnitNumber { get; init; } = [];
    public string[] Multifamily { get; init; } = [];
    public string[] SiteAddress { get; init; } = [];
    public string[] RoomType { get; init; } = [];
    public string[] RoomNumber { get; init; } = [];
    public string[] Substrate { get; init; } = [];
    public string[] Side { get; init; } = [];
    public string[] Condition { get; init; } = [];
    public string[] Timestamp { get; init; } = [];

    public static ExcelColumnMapping Default { get; } = new()
    {
        ReadingId =
        [
            "Reading ID", "ReadingID", "Reading #", "Reading Number", "ID", "Rdg", "Reading", "Test ID",
            "Test #", "Sample ID",
        ],
        Component =
        [
            "Component", "COMPONENT", "Components", "COMPONE", "COMPON", "Building Component", "Comp",
            "Component Type", "Testing Component", "Substrate Component", "Test Component", "Element",
        ],
        Color =
        [
            "Color", "COLOR", "Paint Color", "Colour", "Surface Color", "Coating Color",
        ],
        LeadContent =
        [
            "PbC", "PbC (mg/cm²)", "PbC (mg/cm2)", "Lead Content", "Lead (mg/cm²)", "Lead (mg/cm2)", "Lead",
            "Pb", "Pb Content", "Lead Concentration", "Concentration", "Concentra", "mg/cm²", "mg/cm2",
            "Result", "RESULT", "XRF Result", "Lead Result", "Pb (mg/cm²)",
        ],
        Location =
        [
            "Location", "Full Location", "Test Location", "Unit/Room", "Room/Unit",
        ],
        UnitNumber =
        [
            "Unit", "Unit #", "Unit Number", "Unit No", "Apt", "Apt #", "Apt No", "Apartment", "Apartment #",
            "Apartment Number", "Dwelling", "Dwelling Unit",
        ],
        Multifamily =
        [
            "MULTI FAM", "Multi Fam", "Multifamily", "Multi Family", "MULTIFAMILY",
        ],
        SiteAddress =
        [
            "SITE ADDR", "Site Addr", "Site Address", "SITE ADDRESS", "Address", "Property Address",
        ],
        RoomType =
        [
            "Room Type", "ROOM TY", "RoomType", "Room", "Room Name", "Area", "Area Type", "Space", "Space Type",
        ],
        RoomNumber =
        [
            // No bare "Number" / "#" — they match flag/SIDE columns; see ExcelColumnMapping.ts
            "Room Number", "Room #", "Room Num", "Room No", "Rm #", "Rm No",
        ],
        Substrate =
        [
            "Substrate", "SUBSTRAT", "Subtrate", "Surface", "Material", "Substrate Type", "Surface Type",
            "Base Material",
        ],
        Side =
        [
            "Side", "SIDE", "Surface Side", "A/B", "Face",
        ],
        Condition =
        [
            "Condition", "CONDITIO", "CONDITION", "Paint Condition", "Surface Condition", "Coating Condition",
        ],
        Timestamp =
        [
            "Date", "Time", "DateTime", "Timestamp", "Reading Date", "Test Date", "Date/Time",
        ],
    };

    public static string? FindColumnMatch(IReadOnlyList<string> headers, IEnumerable<string> possibleNames) =>
        FindColumnMatchExcluding(headers, possibleNames, null);

    /// <param name="excludedNormalizedHeaders">Normalized header names that must not be returned (e.g. <c>units</c> for unit number).</param>
    public static string? FindColumnMatchExcluding(
        IReadOnlyList<string> headers,
        IEnumerable<string> possibleNames,
        IReadOnlySet<string>? excludedNormalizedHeaders)
    {
        var normalizedHeaders = headers.Select(h => h.ToLowerInvariant().Trim()).ToList();

        foreach (var name in possibleNames)
        {
            var normalizedName = name.ToLowerInvariant().Trim();
            var index = normalizedHeaders.IndexOf(normalizedName);
            if (index >= 0)
            {
                var nh = normalizedHeaders[index];
                if (excludedNormalizedHeaders != null && excludedNormalizedHeaders.Contains(nh))
                    continue;
                return headers[index];
            }
        }

        const int minPrefixLen = 4;
        for (var i = 0; i < normalizedHeaders.Count; i++)
        {
            var h = normalizedHeaders[i];
            if (string.IsNullOrEmpty(h)) continue;
            if (excludedNormalizedHeaders != null && excludedNormalizedHeaders.Contains(h))
                continue;
            foreach (var name in possibleNames)
            {
                var n = name.ToLowerInvariant().Trim();
                if (h.Length < minPrefixLen && n.Length < minPrefixLen) continue;
                if (n.StartsWith(h, StringComparison.Ordinal) || h.StartsWith(n, StringComparison.Ordinal))
                    return headers[i];
            }
        }

        return null;
    }

    public static IReadOnlyList<string> GetComponentCandidateHeaders(
        IReadOnlyList<string> headers,
        IEnumerable<string> possibleNames)
    {
        var normalized = headers.Select(h => h.ToLowerInvariant().Trim()).ToList();
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string header)
        {
            var key = header.ToLowerInvariant().Trim();
            if (string.IsNullOrEmpty(key) || seen.Contains(key)) return;
            seen.Add(key);
            candidates.Add(header);
        }

        foreach (var name in possibleNames)
        {
            var n = name.ToLowerInvariant().Trim();
            for (var i = 0; i < normalized.Count; i++)
            {
                var h = normalized[i];
                if (string.IsNullOrEmpty(h)) continue;
                if (h == n)
                {
                    Add(headers[i]);
                    break;
                }

                var minLen = Math.Min(4, Math.Min(h.Length, n.Length));
                if (minLen >= 4 && (n.StartsWith(h, StringComparison.Ordinal) || h.StartsWith(n, StringComparison.Ordinal)))
                {
                    Add(headers[i]);
                    break;
                }
            }
        }

        for (var i = 0; i < headers.Count; i++)
        {
            var h = normalized[i];
            if (string.IsNullOrEmpty(h) || seen.Contains(h)) continue;
            if (h.Contains("component", StringComparison.Ordinal) ||
                h.Contains("componer", StringComparison.Ordinal) ||
                h.Contains("components", StringComparison.Ordinal) ||
                (h.Contains("comp", StringComparison.Ordinal) && h.Length > 4))
                Add(headers[i]);
        }

        return candidates;
    }

    public static string? PickBestComponentColumn(
        IReadOnlyList<string> candidateHeaders,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> sampleRows)
    {
        if (candidateHeaders.Count == 0) return null;
        if (candidateHeaders.Count == 1) return candidateHeaders[0];

        var sampleSize = Math.Min(50, sampleRows.Count);
        var bestHeader = candidateHeaders[0];
        var bestScore = -1.0;

        foreach (var header in candidateHeaders)
        {
            var values = new List<string>();
            for (var r = 0; r < sampleSize; r++)
            {
                if (!sampleRows[r].TryGetValue(header, out var v) || v == null) continue;
                var s = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? "";
                if (s.Length > 0) values.Add(s);
            }

            var uniqueCount = values.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var avgLength = values.Count > 0 ? values.Average(v => v.Length) : 0;
            var score = uniqueCount * 2.0 + avgLength;
            if (score > bestScore)
            {
                bestScore = score;
                bestHeader = header;
            }
        }

        return bestHeader;
    }

    public static IReadOnlyList<string> GetUnmappedHeaders(IReadOnlyList<string> headers, ExcelColumnMapping mapping)
    {
        var allPossibleNames = mapping.ReadingId
            .Concat(mapping.Component)
            .Concat(mapping.Color)
            .Concat(mapping.LeadContent)
            .Concat(mapping.Location)
            .Concat(mapping.UnitNumber)
            .Concat(mapping.Multifamily)
            .Concat(mapping.SiteAddress)
            .Concat(mapping.RoomType)
            .Concat(mapping.RoomNumber)
            .Concat(mapping.Substrate)
            .Concat(mapping.Side)
            .Concat(mapping.Condition)
            .Concat(mapping.Timestamp)
            .Select(n => n.ToLowerInvariant().Trim())
            .ToHashSet(StringComparer.Ordinal);

        return headers.Where(h => !allPossibleNames.Contains(h.ToLowerInvariant().Trim())).ToList();
    }
}
