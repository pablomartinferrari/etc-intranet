using Azure.Identity;
using Intranet.Api.MultifamilyLbp.Options;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Intranet.Api.Agentic.Day;

public sealed record DayItem(string Id, string Title, string? Detail, DateTimeOffset? When, string? Href);

public sealed record DaySection(string Id, string Title, IReadOnlyList<DayItem> Items);

/// <summary>
/// Pluggable Your Day source. Add a new implementation and register it in
/// <see cref="Intranet.Api.Agentic.AgenticServiceExtensions"/> — the home page
/// does not need to change.
/// </summary>
public interface IDayDataSource
{
    string Name { get; }

    bool IsEnabled { get; }

    Task<IReadOnlyList<DaySection>> GetAsync(WorkspaceActor actor, DateOnly date, CancellationToken cancellationToken);
}

public interface IDayAggregator
{
    Task<DaySummaryDto> GetAsync(WorkspaceActor actor, DateOnly date, CancellationToken cancellationToken);
}

public sealed class MockDayDataSource : IDayDataSource
{
    public string Name => "mock";

    public bool IsEnabled => true;

    public Task<IReadOnlyList<DaySection>> GetAsync(
        WorkspaceActor actor,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var morning = date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Local);
        var sections = new DaySection[]
        {
            new("meetings", "Meetings",
            [
                new DayItem("m1", "Pipeline standup", "Teams · 9:00–9:30", new DateTimeOffset(morning), null),
                new DayItem("m2", "Project Alpha review", "Conference A · 11:00–12:00", new DateTimeOffset(morning.AddHours(2)), null),
                new DayItem("m3", "1:1 with User B", "15:00–15:30", new DateTimeOffset(morning.AddHours(6)), null),
            ]),
            new("tasks", "Tasks",
            [
                new DayItem("t1", "Send quarterly report draft", "Due today · Folder A", null, "/workspace/repo/" + WorkspaceIds.FolderA),
                new DayItem("t2", "Review close-out reasons", "Pipeline", null, "/pipeline"),
                new DayItem("t3", "Upload inspection photos", "Lead", null, "/lead-inspection"),
                new DayItem("t4", "Share Team chat with Group X", "Workspace", null, "/workspace/chats/" + WorkspaceIds.ChatTeam),
                new DayItem("t5", "Prep Alpha instructions", "Project Alpha", null, "/workspace/projects/" + WorkspaceIds.ProjectAlpha),
            ]),
            new("follow-ups", "Follow-ups",
            [
                new DayItem("f1", "Reply to User C on Team chat", "Waiting since yesterday", null, "/workspace/chats/" + WorkspaceIds.ChatTeam),
                new DayItem("f2", "Confirm Folder B indexing", "Repository", null, "/workspace/repo/" + WorkspaceIds.FolderB),
            ]),
            new("suggestions", "Agent suggestions",
            [
                new DayItem("s1", "Summarize yesterday’s Project Alpha thread", "I can pull citations from Folder A.", null, "/workspace/chats/" + WorkspaceIds.ChatPlaceholder),
                new DayItem("s2", "Draft a follow-up after the 11:00 review", "Uses your day calendar + Alpha instructions.", null, null),
                new DayItem("s3", "Find last quarter’s report in the repository", "Search is scoped to Folder A.", null, "/workspace/search?q=quarterly%20report&folderId=" + WorkspaceIds.FolderA),
            ]),
        };
        return Task.FromResult<IReadOnlyList<DaySection>>(sections);
    }
}

/// <summary>
/// Optional Microsoft Graph calendar overlay. Requires application permission
/// Calendars.Read and <c>Features:AgenticWorkspace:UseGraphCalendar=true</c>.
/// Failures fall back to no meetings — mock tasks still show.
/// </summary>
public sealed class GraphCalendarDayDataSource : IDayDataSource
{
    private readonly AzureAdOptions _azureAd;
    private readonly AgenticOptions _options;
    private readonly ILogger<GraphCalendarDayDataSource> _logger;

    public GraphCalendarDayDataSource(
        IOptions<AzureAdOptions> azureAd,
        IOptions<AgenticOptions> options,
        ILogger<GraphCalendarDayDataSource> logger)
    {
        _azureAd = azureAd.Value;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "graph-calendar";

    public bool IsEnabled =>
        _options.UseGraphCalendar
        && HasValue(_azureAd.TenantId)
        && !string.Equals(_azureAd.TenantId, "common", StringComparison.OrdinalIgnoreCase)
        && HasValue(_azureAd.ClientId)
        && !_azureAd.ClientId.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
        && HasValue(_azureAd.ClientSecret);

    public async Task<IReadOnlyList<DaySection>> GetAsync(
        WorkspaceActor actor,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled || actor.Oid.StartsWith("dev-", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        try
        {
            var credential = new ClientSecretCredential(_azureAd.TenantId, _azureAd.ClientId, _azureAd.ClientSecret);
            var client = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
            var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            var page = await client.Users[actor.Oid].CalendarView.GetAsync(config =>
            {
                config.QueryParameters.StartDateTime = start.ToString("o");
                config.QueryParameters.EndDateTime = end.ToString("o");
                config.QueryParameters.Select = ["id", "subject", "start", "end", "location", "webLink"];
                config.QueryParameters.Top = 8;
                config.QueryParameters.Orderby = ["start/dateTime"];
            }, cancellationToken);

            var items = new List<DayItem>();
            foreach (var ev in page?.Value ?? [])
            {
                if (string.IsNullOrWhiteSpace(ev.Id))
                {
                    continue;
                }

                var when = ParseGraphTime(ev.Start);
                var endAt = ParseGraphTime(ev.End);
                var window = when is null
                    ? ev.Location?.DisplayName
                    : endAt is null
                        ? when.Value.ToString("t")
                        : $"{when.Value.ToString("t")}–{endAt.Value.ToString("t")}";
                items.Add(new DayItem(
                    ev.Id,
                    ev.Subject ?? "Untitled meeting",
                    string.Join(" · ", new[] { ev.Location?.DisplayName, window }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    when,
                    ev.WebLink));
            }

            return items.Count == 0
                ? []
                : [new DaySection("meetings", "Meetings", items)];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph calendar unavailable for {Oid}; using mock meetings.", actor.Oid);
            return [];
        }
    }

    private static DateTimeOffset? ParseGraphTime(DateTimeTimeZone? value)
    {
        if (value?.DateTime is null)
        {
            return null;
        }

        return DateTimeOffset.TryParse(value.DateTime, out var parsed) ? parsed : null;
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed class DayAggregator : IDayAggregator
{
    private readonly IEnumerable<IDayDataSource> _sources;

    public DayAggregator(IEnumerable<IDayDataSource> sources)
    {
        _sources = sources;
    }

    public async Task<DaySummaryDto> GetAsync(WorkspaceActor actor, DateOnly date, CancellationToken cancellationToken)
    {
        var sections = new Dictionary<string, DaySection>(StringComparer.OrdinalIgnoreCase);
        var used = new List<string>();
        foreach (var source in _sources)
        {
            if (!source.IsEnabled)
            {
                continue;
            }

            var batch = await source.GetAsync(actor, date, cancellationToken);
            if (batch.Count == 0)
            {
                continue;
            }

            used.Add(source.Name);
            foreach (var section in batch)
            {
                sections[section.Id] = section;
            }
        }

        var ordered = new[] { "meetings", "tasks", "follow-ups", "suggestions" }
            .Where(sections.ContainsKey)
            .Select(id => ToDto(sections[id]))
            .ToList();

        var hour = DateTimeOffset.Now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
        return new DaySummaryDto(
            greeting,
            actor.DisplayName,
            date,
            ordered,
            used);
    }

    private static DaySectionDto ToDto(DaySection section) =>
        new(
            section.Id,
            section.Title,
            section.Items.Select(i => new DayItemDto(i.Id, i.Title, i.Detail, i.When, i.Href)).ToList());
}
