using System.Globalization;

namespace Intranet.Api.Agentic.Day;

public static class DayTimeZones
{
    public const string Utc = "UTC";
    public const string HeaderName = "X-User-Timezone";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Utc;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 64)
        {
            trimmed = trimmed[..64];
        }

        foreach (var c in trimmed)
        {
            if (char.IsLetterOrDigit(c) || c is '/' or '_' or '-' or '+' or ' ' or '.')
            {
                continue;
            }

            return Utc;
        }

        return TryResolve(trimmed, out _) ? trimmed : Utc;
    }

    public static bool TryResolve(string id, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }

    public static TimeZoneInfo Resolve(string? id) =>
        TryResolve(Normalize(id), out var timeZone) ? timeZone : TimeZoneInfo.Utc;

    public static DateTimeOffset Now(string? timeZoneId)
    {
        var tz = Resolve(timeZoneId);
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
    }

    public static DateTimeOffset At(DateOnly date, int hour, int minute, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    public static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo timeZone) =>
        At(date, 0, 0, timeZone);

    public static DateTimeOffset ExclusiveEndOfDay(DateOnly date, TimeZoneInfo timeZone) =>
        StartOfDay(date.AddDays(1), timeZone);

    public static string FormatClock(DateTimeOffset? value)
    {
        if (value is null)
        {
            return "";
        }

        return value.Value.ToString("t", CultureInfo.GetCultureInfo("en-US"));
    }

    public static string FormatWindow(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start is null)
        {
            return "";
        }

        return end is null
            ? FormatClock(start)
            : $"{FormatClock(start)}–{FormatClock(end)}";
    }
}
