using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Intranet.Api.MultifamilyLbp.Models;
using Intranet.Api.MultifamilyLbp.Services;

namespace Intranet.Api.MultifamilyLbp.Services.Excel;

/// <summary>In-process port of SPFx <c>ExcelParserService</c> (no AI mapping; static headers only).</summary>
public sealed class XrfWorkbookParser : IParserClient
{
    private const double LeadPositiveThreshold = 1.0;
    private const int MinVikenDataRows = 4000;
    private const int VikenExtendedLastRow = 6000;
    private const int VikenHeaderRowIndex = 6;
    private const int MaxHeaderScanRows = 25;

    private readonly ExcelColumnMapping _mapping = ExcelColumnMapping.Default;

    public Task<IReadOnlyList<XrfReadingDto>> ParseFileAsync(
        byte[] fileContent,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var grid = IsCsvFileName(fileName)
            ? ReadCsvGrid(fileContent)
            : ReadXlsxGrid(fileContent);

        if (grid.Count == 0)
            throw new InvalidOperationException("No data found in file.");

        var (headerRowIndex, headers, headerWarnings) = DetectHeaderRow(grid);
        if (headers.Count == 0)
            throw new InvalidOperationException("No header row found.");

        var jsonRows = BuildRowObjects(grid, headerRowIndex, headers);
        if (jsonRows.Count == 0)
            throw new InvalidOperationException("No data rows found below headers.");

        var detected = DetectColumns(headers);

        var componentCandidates = ExcelColumnMapping.GetComponentCandidateHeaders(headers, _mapping.Component);
        if (componentCandidates.Count >= 2 && jsonRows.Count > 0)
        {
            var best = ExcelColumnMapping.PickBestComponentColumn(componentCandidates, jsonRows);
            if (!string.IsNullOrEmpty(best))
                detected["component"] = best;
        }

        var missingRequired = ValidateRequiredColumns(detected);
        if (missingRequired.Count > 0)
        {
            var payload = new
            {
                error = "Column mapping needs confirmation; re-upload from SharePoint web part or extend server mapping.",
                missingRequired,
                headers,
                detectedColumns = detected,
            };
            var body = JsonSerializer.Serialize(payload);
            throw new InvalidOperationException(body);
        }

        var readings = new List<XrfReadingDto>();
        var rowNumber = headerRowIndex + 2;

        for (var i = 0; i < jsonRows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dto = ParseRow(jsonRows[i], rowNumber, detected, i);
            rowNumber++;
            if (dto != null)
                readings.Add(dto);
        }

        return Task.FromResult<IReadOnlyList<XrfReadingDto>>(readings);
    }

    private static bool IsCsvFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".csv" or ".txt";
    }

    private static List<List<object?>> ReadCsvGrid(byte[] fileContent)
    {
        var rows = new List<List<object?>>();
        using var ms = new MemoryStream(fileContent);
        using var reader = new StreamReader(ms);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null,
        };
        using var csv = new CsvReader(reader, config);
        while (csv.Read())
        {
            var row = new List<object?>();
            for (var i = 0; i < csv.Parser.Count; i++)
            {
                var f = csv.GetField(i);
                row.Add(string.IsNullOrEmpty(f) ? "" : f);
            }

            if (row.Count > 0)
                rows.Add(row);
        }

        return rows;
    }

    private List<List<object?>> ReadXlsxGrid(byte[] fileContent)
    {
        using var ms = new MemoryStream(fileContent);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        var firstCellText = Convert.ToString(CellToObject(ws.Cell(1, 1)), CultureInfo.InvariantCulture)?.ToLowerInvariant() ?? "";
        var looksLikeVikenMetadata = firstCellText.Contains("company", StringComparison.Ordinal) ||
                                     firstCellText.Contains("model", StringComparison.Ordinal) ||
                                     firstCellText.Contains("viken", StringComparison.Ordinal) ||
                                     firstCellText.Contains("pb200", StringComparison.Ordinal) ||
                                     firstCellText.Contains("serial", StringComparison.Ordinal);

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow > 0 && lastRow < MinVikenDataRows && looksLikeVikenMetadata)
            lastRow = Math.Max(lastRow, VikenExtendedLastRow);

        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
        if (lastRow <= 0)
            return [];

        var grid = new List<List<object?>>();
        for (var r = 1; r <= lastRow; r++)
        {
            var row = new List<object?>();
            for (var c = 1; c <= lastCol; c++)
                row.Add(CellToObject(ws.Cell(r, c)));

            grid.Add(row);
        }

        return grid;
    }

    /// <summary>Reads cell value without calling <see cref="XLCellValue.GetText"/> on non-text values (throws).</summary>
    private static object? CellToObject(IXLCell cell)
    {
        if (cell.IsEmpty()) return "";
        var v = cell.Value;
        if (v.IsBlank) return "";

        // Prefer typed conversions; GetText() throws unless the underlying value is text.
        // Important: Try double BEFORE DateTime. Small numbers (0.9, 1, 0) are valid mg/cm² but also
        // valid Excel date serials (fractions of a day / 1900-01-01); ClosedXML will accept them as DateTime first.
        // Try double BEFORE bool: many count/ID columns are 1,4,5…; ClosedXML may classify 0/1 as bool first.
        if (v.TryConvert(out double d, CultureInfo.InvariantCulture)) return d;
        if (v.TryConvert(out bool b)) return b;
        if (v.TryConvert(out DateTime dt)) return dt;
        if (v.TryConvert(out TimeSpan ts, CultureInfo.InvariantCulture)) return ts;

        if (v.TryGetText(out var text)) return text;

        try
        {
            var formatted = cell.GetFormattedString();
            return string.IsNullOrEmpty(formatted) ? "" : formatted;
        }
        catch
        {
            return "";
        }
    }

    private static (int headerRowIndex, List<string> headers, List<string> warnings) DetectHeaderRow(
        IReadOnlyList<List<object?>> rawData)
    {
        var warnings = new List<string>();
        var m = ExcelColumnMapping.Default;
        var firstRow = rawData[0];
        var firstCell = firstRow.Count > 0 ? Convert.ToString(firstRow[0], CultureInfo.InvariantCulture)?.ToLowerInvariant() ?? "" : "";
        var looksLikeVikenMetadata = firstCell.Contains("company", StringComparison.Ordinal) ||
                                     firstCell.Contains("model", StringComparison.Ordinal) ||
                                     firstCell.Contains("viken", StringComparison.Ordinal) ||
                                     firstCell.Contains("pb200", StringComparison.Ordinal) ||
                                     firstCell.Contains("serial", StringComparison.Ordinal);

        var headerRowIndex = 0;
        var headers = new List<string>();
        var bestMatchCount = 0;

        for (var i = 0; i < Math.Min(rawData.Count, MaxHeaderScanRows); i++)
        {
            var row = rawData[i];
            if (row.Count == 0) continue;

            var matchCount = row.Count(cell =>
            {
                var s = Convert.ToString(cell, CultureInfo.InvariantCulture)?.ToLowerInvariant().Trim() ?? "";
                if (string.IsNullOrEmpty(s)) return false;
                return m.ReadingId.Any(n => n.ToLowerInvariant() == s) ||
                       m.Component.Any(n => n.ToLowerInvariant() == s) ||
                       m.LeadContent.Any(n => n.ToLowerInvariant() == s) ||
                       m.Color.Any(n => n.ToLowerInvariant() == s);
            });

            if (matchCount >= 2 && matchCount > bestMatchCount)
            {
                bestMatchCount = matchCount;
                headerRowIndex = i;
                headers = row.Select(h => Convert.ToString(h, CultureInfo.InvariantCulture)?.Trim() ?? "").ToList();
            }
        }

        if (looksLikeVikenMetadata && rawData.Count > VikenHeaderRowIndex)
        {
            var row7 = rawData[VikenHeaderRowIndex];
            if (row7.Count > 0)
            {
                var row7MatchCount = row7.Count(cell =>
                {
                    var s = Convert.ToString(cell, CultureInfo.InvariantCulture)?.ToLowerInvariant().Trim() ?? "";
                    if (string.IsNullOrEmpty(s)) return false;
                    return m.ReadingId.Any(n => n.ToLowerInvariant() == s) ||
                           m.Component.Any(n => n.ToLowerInvariant() == s) ||
                           m.LeadContent.Any(n => n.ToLowerInvariant() == s) ||
                           m.Color.Any(n => n.ToLowerInvariant() == s);
                });
                if (row7MatchCount >= 2 && (headerRowIndex < VikenHeaderRowIndex || row7MatchCount >= bestMatchCount))
                {
                    headerRowIndex = VikenHeaderRowIndex;
                    headers = row7.Select(h => Convert.ToString(h, CultureInfo.InvariantCulture)?.Trim() ?? "").ToList();
                    bestMatchCount = row7MatchCount;
                }
            }
        }

        if (headers.Count == 0 && rawData.Count > 0)
        {
            headers = rawData[0].Select(h => Convert.ToString(h, CultureInfo.InvariantCulture)?.Trim() ?? "").ToList();
            warnings.Add("Could not clearly identify header row. Assuming first row contains headers.");
        }
        else if (headerRowIndex > 0)
        {
            warnings.Add(
                $"Detected header row at row {headerRowIndex + 1} ({bestMatchCount} columns matched, skipped {headerRowIndex} row(s) above)");
        }

        return (headerRowIndex, headers, warnings);
    }

    private static List<IReadOnlyDictionary<string, object?>> BuildRowObjects(
        IReadOnlyList<List<object?>> rawData,
        int headerRowIndex,
        IReadOnlyList<string> headers)
    {
        var jsonData = new List<IReadOnlyDictionary<string, object?>>();
        for (var i = headerRowIndex + 1; i < rawData.Count; i++)
        {
            var rowData = rawData[i];
            var obj = new Dictionary<string, object?>(StringComparer.Ordinal);
            var hasData = false;
            for (var j = 0; j < headers.Count; j++)
            {
                var key = headers[j];
                if (string.IsNullOrEmpty(key)) continue;
                var val = j < rowData.Count ? rowData[j] : null;
                obj[key] = val;
                if (val != null && !(val is string s && s.Length == 0))
                    hasData = true;
            }

            if (hasData)
                jsonData.Add(obj);
        }

        return jsonData;
    }

    private Dictionary<string, string> DetectColumns(IReadOnlyList<string> headers)
    {
        var detected = new Dictionary<string, string>(StringComparer.Ordinal);
        var readingIdCol = ExcelColumnMapping.FindColumnMatch(headers, _mapping.ReadingId);
        if (readingIdCol != null) detected["readingId"] = readingIdCol;

        var componentCol = ExcelColumnMapping.FindColumnMatch(headers, _mapping.Component);
        if (componentCol != null) detected["component"] = componentCol;

        var colorCol = ExcelColumnMapping.FindColumnMatch(headers, _mapping.Color);
        if (colorCol != null) detected["color"] = colorCol;

        var concentrationCol = ExcelColumnMapping.FindColumnMatch(headers,
        [
            "Concentration", "Concentra", "Lead Content", "PbC", "PbC (mg/cm²)", "Lead (mg/cm²)", "mg/cm²", "mg/cm2",
        ]);
        var resultCol = ExcelColumnMapping.FindColumnMatch(headers, ["Result", "RESULT", "XRF Result", "Lead Result"]);
        var anyLeadCol = ExcelColumnMapping.FindColumnMatch(headers, _mapping.LeadContent);
        if (concentrationCol != null)
            detected["leadContent"] = concentrationCol;
        else if (resultCol != null)
            detected["leadContent"] = resultCol;
        else if (anyLeadCol != null)
            detected["leadContent"] = anyLeadCol;

        void MapOptional(string key, string[] names)
        {
            var col = ExcelColumnMapping.FindColumnMatch(headers, names);
            if (col != null) detected[key] = col;
        }

        MapOptional("location", _mapping.Location);
        var unitNumberCol = ExcelColumnMapping.FindColumnMatchExcluding(
            headers,
            _mapping.UnitNumber,
            ExcelColumnMapping.UnitNumberMatchExclusions);
        if (unitNumberCol != null) detected["unitNumber"] = unitNumberCol;
        MapOptional("multifamily", _mapping.Multifamily);
        MapOptional("siteAddress", _mapping.SiteAddress);
        MapOptional("roomType", _mapping.RoomType);
        MapOptional("roomNumber", _mapping.RoomNumber);
        MapOptional("substrate", _mapping.Substrate);
        MapOptional("side", _mapping.Side);
        MapOptional("condition", _mapping.Condition);
        MapOptional("timestamp", _mapping.Timestamp);

        return detected;
    }

    private static List<string> ValidateRequiredColumns(IReadOnlyDictionary<string, string> detected)
    {
        string[] required = ["readingId", "component", "color", "leadContent", "substrate"];
        return required.Where(col => !detected.ContainsKey(col) || string.IsNullOrWhiteSpace(detected[col])).ToList();
    }

    private XrfReadingDto? ParseRow(
        IReadOnlyDictionary<string, object?> row,
        int rowNumber,
        IReadOnlyDictionary<string, string> columns,
        int rowIndex)
    {
        var readingIdCol = columns["readingId"];
        var componentCol = columns["component"];
        var colorCol = columns["color"];
        var leadCol = columns["leadContent"];

        var rawReadingId = CellToLogicalString(row.GetValueOrDefault(readingIdCol));
        var rawComponent = CellToLogicalString(row.GetValueOrDefault(componentCol));
        var color = CellToLogicalString(row.GetValueOrDefault(colorCol));
        var leadContentRaw = row.GetValueOrDefault(leadCol);

        var roomType = columns.TryGetValue("roomType", out var rtc)
            ? CellToLogicalString(row.GetValueOrDefault(rtc))
            : "";
        var roomNumber = columns.TryGetValue("roomNumber", out var rnc)
            ? CellToIdentifierString(row.GetValueOrDefault(rnc))
            : "";
        var substrate = columns.TryGetValue("substrate", out var sc)
            ? CellToLogicalString(row.GetValueOrDefault(sc))
            : "";

        if (IsCalibrationRow(rawComponent, leadContentRaw, rawReadingId))
            return null;

        var leadParsed = ParseLeadContent(leadContentRaw);
        if (string.IsNullOrEmpty(rawComponent))
            return null;
        if (leadParsed == null)
            return null;

        var readingId = !string.IsNullOrEmpty(rawReadingId) ? $"{rawReadingId}_{rowIndex}" : $"Row_{rowIndex}";

        string? unitNumber = null;
        if (columns.TryGetValue("unitNumber", out var unc))
            unitNumber = CellToLogicalString(row.GetValueOrDefault(unc)) is { Length: > 0 } u ? u : null;

        string? multifamily = null;
        if (columns.TryGetValue("multifamily", out var mfc))
            multifamily = CellToLogicalString(row.GetValueOrDefault(mfc)) is { Length: > 0 } m ? m : null;

        string? siteAddress = null;
        if (columns.TryGetValue("siteAddress", out var sac))
            siteAddress = CellToLogicalString(row.GetValueOrDefault(sac)) is { Length: > 0 } s ? s : null;

        var location = "";
        if (columns.TryGetValue("location", out var locCol))
            location = CellToLogicalString(row.GetValueOrDefault(locCol));

        if (string.IsNullOrEmpty(location) && (!string.IsNullOrEmpty(unitNumber) || !string.IsNullOrEmpty(roomType) ||
                                                 !string.IsNullOrEmpty(roomNumber)))
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(unitNumber)) parts.Add($"Unit {unitNumber}");
            if (!string.IsNullOrEmpty(roomType))
                parts.Add(!string.IsNullOrEmpty(roomNumber) ? $"{roomType} {roomNumber}" : roomType);
            else if (!string.IsNullOrEmpty(roomNumber)) parts.Add($"Room {roomNumber}");
            location = string.Join(" - ", parts);
        }

        var ts = columns.TryGetValue("timestamp", out var tsc) ? ParseTimestamp(row.GetValueOrDefault(tsc)) : null;

        var rawDict = row.ToDictionary(k => k.Key, k => SanitizeRawValue(k.Value), StringComparer.Ordinal);
        rawDict["originalReadingId"] = string.IsNullOrEmpty(rawReadingId) ? null : rawReadingId;

        return new XrfReadingDto
        {
            ReadingId = readingId,
            Component = rawComponent,
            Color = string.IsNullOrEmpty(color) ? "Unknown" : color,
            LeadContent = leadParsed.Value,
            IsPositive = leadParsed.Value >= LeadPositiveThreshold,
            Location = location,
            UnitNumber = unitNumber,
            Multifamily = multifamily,
            SiteAddress = siteAddress,
            RoomType = string.IsNullOrEmpty(roomType) ? null : roomType,
            RoomNumber = string.IsNullOrEmpty(roomNumber) ? null : roomNumber,
            Substrate = string.IsNullOrEmpty(substrate) ? null : substrate,
            Side = columns.TryGetValue("side", out var sideCol)
                ? CellToLogicalString(row.GetValueOrDefault(sideCol)) is { Length: > 0 } sd ? sd : null
                : null,
            Condition = columns.TryGetValue("condition", out var condCol)
                ? CellToLogicalString(row.GetValueOrDefault(condCol)) is { Length: > 0 } cd ? cd : null
                : null,
            Timestamp = ts?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            RawRow = JsonSerializer.SerializeToElement(rawDict),
        };
    }

    private static object? SanitizeRawValue(object? v)
    {
        if (v == null) return null;
        if (v is DateTime dt) return dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        return v;
    }

    /// <summary>Excel flags in text/ID columns become <c>True</c>/<c>False</c> strings — treat as empty.</summary>
    private static string CellToLogicalString(object? v)
    {
        if (v == null) return "";
        if (v is bool) return "";
        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("false", StringComparison.OrdinalIgnoreCase))
            return "";
        return s;
    }

    /// <summary>Room # and similar: numeric cells as invariant string; bool and "true"/"false" text as empty.</summary>
    private static string CellToIdentifierString(object? v)
    {
        if (v == null) return "";
        if (v is bool) return "";
        switch (v)
        {
            case int n:
                return n.ToString(CultureInfo.InvariantCulture);
            case long ln:
                return ln.ToString(CultureInfo.InvariantCulture);
            case double d when double.IsFinite(d):
                var td = Math.Truncate(d);
                return Math.Abs(d - td) < 1e-9
                    ? td.ToString(CultureInfo.InvariantCulture)
                    : d.ToString("G", CultureInfo.InvariantCulture);
            case float f when float.IsFinite(f):
                var tf = Math.Truncate(f);
                return Math.Abs(f - tf) < 1e-5f
                    ? tf.ToString(CultureInfo.InvariantCulture)
                    : f.ToString("G", CultureInfo.InvariantCulture);
            case decimal dec:
                return dec == decimal.Truncate(dec)
                    ? decimal.Truncate(dec).ToString(CultureInfo.InvariantCulture)
                    : dec.ToString(CultureInfo.InvariantCulture);
        }

        var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("false", StringComparison.OrdinalIgnoreCase))
            return "";
        return s;
    }

    private static double? ParseLeadContent(object? value)
    {
        switch (value)
        {
            case double d:
                return d;
            case float f:
                return f;
            case int n:
                return n;
            case long l:
                return l;
            case decimal m:
                return (double)m;
            case bool b:
                // Excel often stores Result / pass-fail columns as TRUE/FALSE; same type can appear in other cols.
                return b ? LeadPositiveThreshold + 0.05 : 0;
        }

        if (value is string str)
        {
            var lowerVal = str.ToLowerInvariant().Trim();
            if (lowerVal is "true" or "false")
                return lowerVal == "true" ? LeadPositiveThreshold + 0.05 : 0;
            if (lowerVal is "pos" or "positive" or "assumed" or "assumed positive")
                return LeadPositiveThreshold + 0.05;
            if (lowerVal is "neg" or "negative" or "n/a" or "-")
                return 0;

            var cleaned = Regex.Replace(str, @"mg/cm[²2]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"ppm", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"[<>]", "");
            cleaned = cleaned.Replace(",", "").Trim();
            return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : null;
        }

        return null;
    }

    private static DateTime? ParseTimestamp(object? value)
    {
        if (value == null) return null;
        if (value is DateTime dt) return dt;
        if (value is double d)
        {
            var excelEpoch = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);
            return excelEpoch.AddDays(d);
        }

        if (value is string s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return parsed;
        return null;
    }

    private bool IsCalibrationRow(string component, object? leadContent, string readingId)
    {
        var lowerComp = component.ToLowerInvariant().Trim();
        var lowerId = readingId.ToLowerInvariant().Trim();
        if (lowerComp.Contains("calibrate", StringComparison.Ordinal) ||
            lowerComp.Contains("calib", StringComparison.Ordinal) ||
            lowerComp is "cal" or "cal." ||
            lowerComp.Contains("standard", StringComparison.Ordinal) ||
            lowerId.Contains("calibrate", StringComparison.Ordinal) ||
            lowerId.Contains("calib", StringComparison.Ordinal))
            return true;

        if (string.IsNullOrEmpty(component))
        {
            var val = ParseLeadContent(leadContent);
            if (val is >= 0.8 and <= 1.2)
                return true;
        }

        return false;
    }
}
