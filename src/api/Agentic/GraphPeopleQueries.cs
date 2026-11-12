using System.Net.Mail;
using System.Text.RegularExpressions;
using Intranet.Api.Agentic.Day;
using Microsoft.Graph.Models.ODataErrors;

namespace Intranet.Api.Agentic;

/// <summary>
/// Directory search query shapes for Workspace Share.
/// Prefer <c>$search</c> with <c>ConsistencyLevel: eventual</c> and <c>$count=true</c>
/// (matches displayName/mail anywhere, not only prefix). Fall back to
/// <c>$filter=startswith(...)</c> when $search is rejected.
/// </summary>
public static class GraphPeopleQueries
{
    public const int Top = 8;
    public const int MinQueryLength = 2;

    public static readonly string[] UserSelect = ["id", "displayName", "mail", "userPrincipalName"];
    public static readonly string[] GroupSelect = ["id", "displayName", "mail"];

    public static string UserSearch(string query)
    {
        var escaped = EscapeSearch(query);
        return $"\"displayName:{escaped}\" OR \"mail:{escaped}\"";
    }

    public static string GroupSearch(string query)
    {
        var escaped = EscapeSearch(query);
        return $"\"displayName:{escaped}\"";
    }

    public static string UserFilter(string query)
    {
        var escaped = EscapeFilter(query);
        return $"startswith(displayName,'{escaped}') or startswith(mail,'{escaped}') or startswith(userPrincipalName,'{escaped}')";
    }

    public static string GroupFilter(string query)
    {
        var escaped = EscapeFilter(query);
        return $"startswith(displayName,'{escaped}')";
    }

    public static bool LooksLikeEmail(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < 3 || !trimmed.Contains('@', StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(trimmed);
            return true;
        }
        catch (FormatException)
        {
            return Regex.IsMatch(trimmed, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }
    }

    public static string EscapeSearch(string query) =>
        query.Trim().Replace("\"", "", StringComparison.Ordinal).Replace("\\", "", StringComparison.Ordinal);

    public static string EscapeFilter(string query) =>
        query.Trim().Replace("'", "''", StringComparison.Ordinal);

    public static string? ErrorCode(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is ODataError odata && !string.IsNullOrWhiteSpace(odata.Error?.Code))
            {
                return odata.Error.Code;
            }
        }

        return GraphMailQueries.ErrorCode(ex);
    }

    public static bool ShouldFallBackFromSearch(Exception ex)
    {
        var code = ErrorCode(ex) ?? "";
        return code.Equals("ErrorInefficientFilter", StringComparison.OrdinalIgnoreCase)
            || code.Equals("Request_UnsupportedQuery", StringComparison.OrdinalIgnoreCase)
            || code.Equals("BadRequest", StringComparison.OrdinalIgnoreCase)
            || (ex.Message ?? "").Contains("ConsistencyLevel", StringComparison.OrdinalIgnoreCase)
            || (ex.Message ?? "").Contains("$search", StringComparison.OrdinalIgnoreCase);
    }
}
