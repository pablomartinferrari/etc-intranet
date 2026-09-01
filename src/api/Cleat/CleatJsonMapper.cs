using System.Globalization;
using System.Text.Json;

namespace Intranet.Api.Cleat;

/// <summary>
/// CLEATUS REST OpenAPI response schemas are empty objects. Field names are taken from
/// the Zapier opportunity payload (the only documented object shape), plus query/param
/// descriptions on GET /v1/recommendations (min_score, has_more, in_pipeline).
/// Missing or oddly-typed fields are ignored rather than throwing.
/// </summary>
public static class CleatJsonMapper
{
    public static RecommendationListDto MapRecommendations(JsonElement root, string appBaseUrl)
    {
        var items = new List<OpportunityDto>();
        foreach (var element in EnumerateItems(root))
        {
            var mapped = TryMapOpportunity(element, appBaseUrl);
            if (mapped is not null)
            {
                items.Add(mapped);
            }
        }

        return new RecommendationListDto
        {
            Items = items,
            HasMore = ReadBool(IndexObject(root), "hasmore", "has_more") ?? false,
            NextCursor = ReadString(IndexObject(root), "nextcursor", "cursor", "next_cursor"),
        };
    }

    public static OpportunityDto? TryMapOpportunity(JsonElement root, string appBaseUrl)
    {
        if (root.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var fields = FlattenOpportunity(root);
        var id = ReadString(fields, "id", "opportunityid", "opportunity_id", "contractid", "contract_id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return new OpportunityDto
        {
            Id = id,
            Title = ReadString(fields, "title", "pursuittitle", "pursuit_title", "name"),
            Agency = FirstNonBlank(
                ReadString(fields, "agencyname", "agency_name", "agency"),
                ReadString(fields, "agencyorgname", "agency_org_name")),
            Naics = ReadNaics(fields),
            Score = ReadScore(fields),
            PostedDate = ReadString(fields, "posteddate", "posted_date"),
            DeadlineDate = ReadString(fields, "deadlinedate", "deadline_date", "duedate", "due_date"),
            SolicitationNumber = ReadString(fields, "solicitationnumber", "solicitation_number"),
            SetAside = FirstNonBlank(
                ReadString(fields, "typeofsetasidedescription", "type_of_set_aside_description"),
                ReadString(fields, "typeofsetaside", "type_of_set_aside", "setaside", "set_aside")),
            Summary = ReadString(fields, "summary"),
            Overview = ReadString(fields, "overview"),
            Description = ReadString(fields, "description"),
            ResponseType = ReadString(fields, "responsetype", "response_type"),
            OpportunityType = ReadString(fields, "type", "opportunitytype", "opportunity_type"),
            PlaceOfPerformance = ReadPlace(fields),
            MatchReason = ReadString(fields, "matchreason", "match_reason", "fitrationale", "fit_rationale"),
            InPipeline = ReadBool(fields, "inpipeline", "in_pipeline"),
            CleatusUrl = ResolveCleatusUrl(fields, id, appBaseUrl),
            SourceUrl = ReadString(fields, "sourceurl", "source_url", "providerurl", "provider_url"),
        };
    }

    public static string BuildCleatusUrl(string opportunityId, string appBaseUrl)
    {
        var origin = string.IsNullOrWhiteSpace(appBaseUrl) ? "https://www.cleat.ai" : appBaseUrl.TrimEnd('/');
        // Authenticated app lives under /dashboard (robots.txt). OpenAPI does not document a permalink.
        return $"{origin}/dashboard/contracts/{Uri.EscapeDataString(opportunityId)}";
    }

    public static string BuildCleatusPursuitUrl(string pursuitId, string appBaseUrl)
    {
        var origin = string.IsNullOrWhiteSpace(appBaseUrl) ? "https://www.cleat.ai" : appBaseUrl.TrimEnd('/');
        return $"{origin}/dashboard/pipeline/{Uri.EscapeDataString(pursuitId)}";
    }

    public static PursuitListDto MapPipelineSearch(JsonElement root, string appBaseUrl)
    {
        var items = new List<PursuitDto>();
        foreach (var element in EnumerateItems(root))
        {
            var mapped = TryMapPursuit(element, appBaseUrl);
            if (mapped is not null)
            {
                items.Add(mapped);
            }
        }

        return new PursuitListDto
        {
            Items = items,
            HasMore = ReadBool(IndexObject(root), "hasmore", "has_more") ?? false,
            NextCursor = ReadString(IndexObject(root), "nextcursor", "cursor", "next_cursor"),
        };
    }

    public static PursuitDto? TryMapPursuit(JsonElement root, string appBaseUrl)
    {
        if (root.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var fields = FlattenPursuit(root);
        var id = ReadString(fields, "id", "pursuitid", "pursuit_id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var opportunityId = ReadString(
            fields,
            "opportunityid",
            "opportunity_id",
            "contractid",
            "contract_id");
        if (string.Equals(opportunityId, id, StringComparison.OrdinalIgnoreCase)
            && id.StartsWith("pur_", StringComparison.OrdinalIgnoreCase))
        {
            opportunityId = null;
        }

        var lastActivity = ReadString(
            fields,
            "updatedat",
            "updated_at",
            "lastactivity",
            "last_activity",
            "lastactivityat",
            "last_activity_at",
            "modifiedat",
            "modified_at");

        var assignee = ReadAssignee(fields);
        var cleatusUrl = !string.IsNullOrWhiteSpace(opportunityId)
            ? ResolveCleatusUrl(fields, opportunityId, appBaseUrl)
            : BuildCleatusPursuitUrl(id, appBaseUrl);

        return new PursuitDto
        {
            Id = id,
            OpportunityId = opportunityId,
            Title = ReadString(fields, "pursuittitle", "pursuit_title", "title", "name"),
            Agency = FirstNonBlank(
                ReadString(fields, "agencyname", "agency_name", "agency"),
                ReadString(fields, "agencyorgname", "agency_org_name")),
            Phase = ReadString(fields, "phase", "status"),
            ColumnTitle = ReadString(fields, "columntitle", "column_title"),
            Archived = ReadBool(fields, "archived", "isarchived", "is_archived") ?? false,
            Favorite = ReadBool(fields, "favorite", "isfavorite", "is_favorite"),
            DeadlineDate = ReadString(fields, "deadlinedate", "deadline_date", "duedate", "due_date"),
            PostedDate = ReadString(fields, "posteddate", "posted_date"),
            SolicitationNumber = ReadString(fields, "solicitationnumber", "solicitation_number"),
            Naics = ReadNaics(fields),
            SetAside = FirstNonBlank(
                ReadString(fields, "typeofsetasidedescription", "type_of_set_aside_description"),
                ReadString(fields, "typeofsetaside", "type_of_set_aside", "setaside", "set_aside")),
            Summary = ReadString(fields, "summary"),
            Overview = ReadString(fields, "overview"),
            Description = ReadString(fields, "description"),
            Assignee = assignee,
            CreatedAt = ReadString(fields, "pursuitcreatedat", "pursuit_created_at", "createdat", "created_at"),
            LastActivityAt = lastActivity,
            LastActivityAvailable = lastActivity is not null,
            CleatusUrl = cleatusUrl,
            SourceUrl = ReadString(fields, "sourceurl", "source_url", "providerurl", "provider_url"),
        };
    }

    private static Dictionary<string, JsonElement> FlattenPursuit(JsonElement root)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        if (TryGetIgnoreCase(root, "data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && !HasLikelyOpportunityFields(root)
            && !TryGetIgnoreCase(root, "phase", out _))
        {
            return FlattenPursuit(data);
        }

        Merge(map, IndexObject(root));

        foreach (var nestedName in new[] { "contract", "opportunity" })
        {
            if (TryGetIgnoreCase(root, nestedName, out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                var nestedMap = IndexObject(nested);
                if (nestedMap.TryGetValue("id", out var nestedId) || nestedMap.TryGetValue("Id", out nestedId))
                {
                    map["contractid"] = nestedId;
                    map["contract_id"] = nestedId;
                    map["opportunityid"] = nestedId;
                    map["opportunity_id"] = nestedId;
                }

                foreach (var pair in nestedMap)
                {
                    if (string.Equals(Normalize(pair.Key), "id", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    map[pair.Key] = pair.Value;
                }
            }
        }

        Restore(map, IndexObject(root),
            "id", "pursuitid", "pursuit_id",
            "phase", "status",
            "columntitle", "column_title", "columnid", "column_id",
            "archived", "favorite",
            "pursuittitle", "pursuit_title",
            "createdat", "created_at", "pursuitcreatedat", "pursuit_created_at",
            "updatedat", "updated_at", "lastactivity", "last_activity");

        return map;
    }

    private static string? ReadAssignee(Dictionary<string, JsonElement> fields)
    {
        var direct = ReadString(
            fields,
            "assignee",
            "assigneename",
            "assignee_name",
            "assignedto",
            "assigned_to",
            "owner",
            "ownername",
            "owner_name",
            "owneremail",
            "owner_email");
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        foreach (var name in new[] { "assignees", "assignedto", "assigned_to", "owners" })
        {
            if (!(fields.TryGetValue(name, out var value) || fields.TryGetValue(Normalize(name), out value)))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var parts = value.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.Object
                        ? StringifyNameObject(item)
                        : Stringify(item))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
                if (parts.Length > 0)
                {
                    return string.Join(", ", parts);
                }
            }
            else if (value.ValueKind == JsonValueKind.Object)
            {
                var nested = StringifyNameObject(value);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string? StringifyNameObject(JsonElement obj)
    {
        var fields = IndexObject(obj);
        return FirstNonBlank(
            ReadString(fields, "fullname", "full_name", "name", "email", "label", "title"));
    }

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var name in new[] { "items", "recommendations", "results", "opportunities", "pursuits", "pipeline", "data" })
        {
            if (TryGetIgnoreCase(root, name, out var candidate) && candidate.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in candidate.EnumerateArray())
                {
                    yield return item;
                }

                yield break;
            }
        }

        if (TryGetIgnoreCase(root, "data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in EnumerateItems(data))
            {
                yield return item;
            }

            yield break;
        }

        yield return root;
    }

    private static Dictionary<string, JsonElement> FlattenOpportunity(JsonElement root)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        // Zapier-style envelope: { event, data: { contract, matchReason, ... } }
        if (TryGetIgnoreCase(root, "data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && !HasLikelyOpportunityFields(root))
        {
            return FlattenOpportunity(data);
        }

        Merge(map, IndexObject(root));

        foreach (var nestedName in new[] { "contract", "opportunity" })
        {
            if (TryGetIgnoreCase(root, nestedName, out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                Merge(map, IndexObject(nested));
            }
        }

        // Recommendation wrapper fields must not be overwritten by nested contract.
        Restore(map, IndexObject(root),
            "matchscore", "match_score", "score",
            "matchreason", "match_reason",
            "inpipeline", "in_pipeline");

        return map;
    }

    private static bool HasLikelyOpportunityFields(JsonElement obj) =>
        TryGetIgnoreCase(obj, "title", out _)
        || TryGetIgnoreCase(obj, "contract", out _)
        || TryGetIgnoreCase(obj, "opportunity", out _)
        || TryGetIgnoreCase(obj, "agencyName", out _)
        || TryGetIgnoreCase(obj, "agency_name", out _);

    private static void Restore(
        Dictionary<string, JsonElement> target,
        Dictionary<string, JsonElement> parent,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (parent.TryGetValue(name, out var value) || parent.TryGetValue(Normalize(name), out value))
            {
                target[name] = value;
                target[Normalize(name)] = value;
            }
        }
    }

    private static Dictionary<string, JsonElement> IndexObject(JsonElement obj)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var property in obj.EnumerateObject())
        {
            map[property.Name] = property.Value;
            map[Normalize(property.Name)] = property.Value;
        }

        return map;
    }

    private static void Merge(Dictionary<string, JsonElement> target, Dictionary<string, JsonElement> source)
    {
        foreach (var pair in source)
        {
            target[pair.Key] = pair.Value;
        }
    }

    private static string Normalize(string name) =>
        name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static bool TryGetIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(Dictionary<string, JsonElement> fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var value) || fields.TryGetValue(Normalize(name), out value))
            {
                var text = Stringify(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }

        return null;
    }

    private static bool? ReadBool(Dictionary<string, JsonElement> fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (!(fields.TryGetValue(name, out var value) || fields.TryGetValue(Normalize(name), out value)))
            {
                continue;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed):
                    return parsed;
                case JsonValueKind.Number when value.TryGetInt32(out var number):
                    return number != 0;
            }
        }

        return null;
    }

    private static double? ReadScore(Dictionary<string, JsonElement> fields)
    {
        foreach (var name in new[] { "matchscore", "match_score", "matchquality", "match_quality", "score" })
        {
            var number = ReadNumber(fields, name);
            if (number is not null)
            {
                return number;
            }
        }

        return null;
    }

    private static double? ReadNumber(Dictionary<string, JsonElement> fields, string name)
    {
        if (!(fields.TryGetValue(name, out var value) || fields.TryGetValue(Normalize(name), out value)))
        {
            return null;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetDouble(out var number):
                return number;
            case JsonValueKind.String when double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed):
                return parsed;
            default:
                return null;
        }
    }

    private static string? ReadNaics(Dictionary<string, JsonElement> fields)
    {
        var single = ReadString(fields, "naicsid", "naics_id", "naics", "naicscode", "naics_code");
        if (!string.IsNullOrWhiteSpace(single))
        {
            return single;
        }

        foreach (var name in new[] { "naicscodes", "naics_codes" })
        {
            if (!(fields.TryGetValue(name, out var value) || fields.TryGetValue(Normalize(name), out value)))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var parts = value.EnumerateArray()
                    .Select(Stringify)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
                if (parts.Length > 0)
                {
                    return string.Join(", ", parts);
                }
            }
        }

        return null;
    }

    private static string? ReadPlace(Dictionary<string, JsonElement> fields)
    {
        var city = ReadString(fields, "placeofperformancecityname", "place_of_performance_city_name", "officecity", "office_city");
        var state = ReadString(
            fields,
            "placeofperformancecitystatename",
            "place_of_performance_city_state_name",
            "placeofperformancecitystatecode",
            "place_of_performance_city_state_code",
            "officestate",
            "office_state");
        var zip = ReadString(fields, "placeofperformancezipcode", "place_of_performance_zipcode", "officezipcode", "office_zipcode");

        var parts = new[] { city, state, zip }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    private static string ResolveCleatusUrl(Dictionary<string, JsonElement> fields, string id, string appBaseUrl)
    {
        foreach (var name in new[] { "cleatusurl", "cleatus_url", "weburl", "web_url", "permalink", "url", "href" })
        {
            var candidate = ReadString(fields, name);
            if (!string.IsNullOrWhiteSpace(candidate) &&
                Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                uri.Host.Contains("cleat.ai", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return BuildCleatusUrl(id, appBaseUrl);
    }

    private static string? Stringify(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
