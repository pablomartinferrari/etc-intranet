using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.Cleat;

public sealed class PipelineService(
    CleatClient cleat,
    IntranetDbContext db,
    ILogger<PipelineService> logger)
{
    private static readonly string[] SearchStatuses = ["active", "won", "archived"];

    public async Task<PipelineDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var pursuits = await LoadAllPursuitsAsync(cancellationToken);
        await FillMissingDeadlinesAsync(pursuits, cancellationToken);

        var closeouts = await db.PursuitCloseouts
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var byPursuit = closeouts.ToDictionary(c => c.PursuitId, StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var items = new List<PipelineItemDto>(pursuits.Count);
        foreach (var pursuit in pursuits.Values.OrderBy(p => p.DeadlineDate ?? "9999"))
        {
            var (needsCloseOut, reasons) = PipelineCloseoutRules.Evaluate(pursuit, now);
            byPursuit.TryGetValue(pursuit.Id, out var row);
            items.Add(new PipelineItemDto
            {
                Pursuit = pursuit,
                NeedsCloseOut = needsCloseOut,
                CloseOutReasons = reasons,
                Closeout = row is null ? null : ToDto(row),
            });
        }

        return new PipelineDashboardDto
        {
            Items = items,
            NeedsCloseOut = items.Where(i => i.NeedsCloseOut).ToList(),
            Counts = Count(items),
            LastActivityFieldFound = items.Any(i => i.Pursuit.LastActivityAvailable),
            AssigneeFieldFound = items.Any(i => !string.IsNullOrWhiteSpace(i.Pursuit.Assignee)),
        };
    }

    public async Task<CloseoutResponse> CloseOutAsync(
        string pursuitId,
        CloseoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!CleatClient.IsValidOpportunityId(pursuitId))
        {
            throw new ArgumentException("Pursuit id looks invalid.");
        }

        var outcome = request.Outcome?.Trim().ToLowerInvariant();
        var reason = string.IsNullOrWhiteSpace(request.ReasonCode) ? null : request.ReasonCode.Trim().ToLowerInvariant();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 2000 })
        {
            note = note[..2000];
        }

        var validation = CloseoutCatalog.Validate(outcome, reason, requireLostReason: true);
        if (validation is not null)
        {
            throw new ArgumentException(validation);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = await db.PursuitCloseouts
            .FirstOrDefaultAsync(c => c.PursuitId == pursuitId, cancellationToken);
        if (entity is null)
        {
            entity = new PursuitCloseout
            {
                PursuitId = pursuitId,
                OpportunityId = request.OpportunityId,
                Outcome = outcome!,
                ReasonCode = reason,
                Note = note,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.PursuitCloseouts.Add(entity);
        }
        else
        {
            entity.OpportunityId = request.OpportunityId ?? entity.OpportunityId;
            entity.Outcome = outcome!;
            entity.ReasonCode = reason;
            entity.Note = note;
            entity.UpdatedAt = now;
            entity.CleatusSyncedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await PatchCleatusAsync(pursuitId, outcome!, cancellationToken);
            entity.CleatusSyncedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return new CloseoutResponse
            {
                CleatusUpdated = true,
                Closeout = ToDto(entity),
            };
        }
        catch (Exception ex) when (ex is CleatNotConfiguredException or CleatUpstreamException)
        {
            logger.LogWarning(ex, "CLEATUS close-out write failed for pursuit {PursuitId}", pursuitId);
            return new CloseoutResponse
            {
                Error = ex is CleatNotConfiguredException
                    ? "cleat_api_key_missing"
                    : ((CleatUpstreamException)ex).ErrorCode,
                Message = ex is CleatNotConfiguredException
                    ? CleatNotConfiguredException.UserMessage
                    : ex.Message,
                CleatusUpdated = false,
                Closeout = ToDto(entity),
            };
        }
    }

    private async Task<Dictionary<string, PursuitDto>> LoadAllPursuitsAsync(CancellationToken cancellationToken)
    {
        var byId = new Dictionary<string, PursuitDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var status in SearchStatuses)
        {
            string? cursor = null;
            var pages = 0;
            do
            {
                var page = await cleat.SearchPipelineAsync(status, cursor, limit: 50, cancellationToken);
                foreach (var item in page.Items)
                {
                    byId[item.Id] = ApplySearchStatus(item, status);
                }

                cursor = page.HasMore ? page.NextCursor : null;
                pages++;
            }
            while (!string.IsNullOrWhiteSpace(cursor) && pages < 20);
        }

        return byId;
    }

    private static PursuitDto ApplySearchStatus(PursuitDto item, string status)
    {
        if (status == "archived" && !item.Archived)
        {
            return new PursuitDto
            {
                Id = item.Id,
                OpportunityId = item.OpportunityId,
                Title = item.Title,
                Agency = item.Agency,
                Phase = item.Phase,
                ColumnTitle = item.ColumnTitle,
                Archived = true,
                Favorite = item.Favorite,
                DeadlineDate = item.DeadlineDate,
                PostedDate = item.PostedDate,
                SolicitationNumber = item.SolicitationNumber,
                Naics = item.Naics,
                SetAside = item.SetAside,
                Summary = item.Summary,
                Overview = item.Overview,
                Description = item.Description,
                Assignee = item.Assignee,
                CreatedAt = item.CreatedAt,
                LastActivityAt = item.LastActivityAt,
                LastActivityAvailable = item.LastActivityAvailable,
                CleatusUrl = item.CleatusUrl,
                SourceUrl = item.SourceUrl,
            };
        }

        if (status == "won" && string.IsNullOrWhiteSpace(item.Phase))
        {
            return new PursuitDto
            {
                Id = item.Id,
                OpportunityId = item.OpportunityId,
                Title = item.Title,
                Agency = item.Agency,
                Phase = "won",
                ColumnTitle = item.ColumnTitle ?? "Won",
                Archived = item.Archived,
                Favorite = item.Favorite,
                DeadlineDate = item.DeadlineDate,
                PostedDate = item.PostedDate,
                SolicitationNumber = item.SolicitationNumber,
                Naics = item.Naics,
                SetAside = item.SetAside,
                Summary = item.Summary,
                Overview = item.Overview,
                Description = item.Description,
                Assignee = item.Assignee,
                CreatedAt = item.CreatedAt,
                LastActivityAt = item.LastActivityAt,
                LastActivityAvailable = item.LastActivityAvailable,
                CleatusUrl = item.CleatusUrl,
                SourceUrl = item.SourceUrl,
            };
        }

        return item;
    }

    private async Task FillMissingDeadlinesAsync(
        Dictionary<string, PursuitDto> pursuits,
        CancellationToken cancellationToken)
    {
        var missing = pursuits.Values
            .Where(p => string.IsNullOrWhiteSpace(p.DeadlineDate) && !string.IsNullOrWhiteSpace(p.OpportunityId))
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        using var gate = new SemaphoreSlim(5, 5);
        var updates = new List<(string Id, PursuitDto Updated)>();
        var tasks = missing.Select(async pursuit =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var opportunity = await cleat.GetOpportunityAsync(pursuit.OpportunityId!, cancellationToken);
                if (opportunity is null)
                {
                    return;
                }

                lock (updates)
                {
                    updates.Add((pursuit.Id, MergeOpportunity(pursuit, opportunity)));
                }
            }
            catch (CleatUpstreamException ex)
            {
                logger.LogWarning(
                    "Could not join opportunity {OpportunityId} for pursuit {PursuitId}: {Code}",
                    pursuit.OpportunityId,
                    pursuit.Id,
                    ex.ErrorCode);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        foreach (var (id, updated) in updates)
        {
            pursuits[id] = updated;
        }
    }

    private static PursuitDto MergeOpportunity(PursuitDto pursuit, OpportunityDto opportunity) =>
        new()
        {
            Id = pursuit.Id,
            OpportunityId = pursuit.OpportunityId ?? opportunity.Id,
            Title = pursuit.Title ?? opportunity.Title,
            Agency = pursuit.Agency ?? opportunity.Agency,
            Phase = pursuit.Phase,
            ColumnTitle = pursuit.ColumnTitle,
            Archived = pursuit.Archived,
            Favorite = pursuit.Favorite,
            DeadlineDate = pursuit.DeadlineDate ?? opportunity.DeadlineDate,
            PostedDate = pursuit.PostedDate ?? opportunity.PostedDate,
            SolicitationNumber = pursuit.SolicitationNumber ?? opportunity.SolicitationNumber,
            Naics = pursuit.Naics ?? opportunity.Naics,
            SetAside = pursuit.SetAside ?? opportunity.SetAside,
            Summary = pursuit.Summary ?? opportunity.Summary,
            Overview = pursuit.Overview ?? opportunity.Overview,
            Description = pursuit.Description ?? opportunity.Description,
            Assignee = pursuit.Assignee,
            CreatedAt = pursuit.CreatedAt,
            LastActivityAt = pursuit.LastActivityAt,
            LastActivityAvailable = pursuit.LastActivityAvailable,
            CleatusUrl = pursuit.CleatusUrl ?? opportunity.CleatusUrl,
            SourceUrl = pursuit.SourceUrl ?? opportunity.SourceUrl,
        };

    private async Task PatchCleatusAsync(string pursuitId, string outcome, CancellationToken cancellationToken)
    {
        // Live PATCH /v1/pursuits/{id} accepts column_id (board title) and archived.
        // It does NOT accept a phase property (additionalProperties: false).
        switch (outcome)
        {
            case CloseoutCatalog.OutcomeWon:
                await cleat.UpdatePursuitAsync(pursuitId, columnTitle: "Won", archived: null, cancellationToken);
                break;
            case CloseoutCatalog.OutcomeLost:
                await cleat.UpdatePursuitAsync(pursuitId, columnTitle: "Lost", archived: null, cancellationToken);
                break;
            default:
                await cleat.UpdatePursuitAsync(pursuitId, columnTitle: null, archived: true, cancellationToken);
                break;
        }
    }

    private static PipelineCountsDto Count(IReadOnlyList<PipelineItemDto> items)
    {
        int triage = 0, preparing = 0, submitted = 0, won = 0, lost = 0, archived = 0, other = 0;
        foreach (var item in items)
        {
            if (item.Pursuit.Archived)
            {
                archived++;
                continue;
            }

            var stage = PipelineCloseoutRules.Normalize(item.Pursuit.Phase)
                ?? PipelineCloseoutRules.Normalize(item.Pursuit.ColumnTitle);
            switch (stage)
            {
                case "triage":
                    triage++;
                    break;
                case "preparing":
                    preparing++;
                    break;
                case "submitted":
                    submitted++;
                    break;
                case "won":
                    won++;
                    break;
                case "lost":
                    lost++;
                    break;
                default:
                    other++;
                    break;
            }
        }

        return new PipelineCountsDto
        {
            Triage = triage,
            Preparing = preparing,
            Submitted = submitted,
            Won = won,
            Lost = lost,
            Archived = archived,
            Other = other,
            Total = items.Count,
        };
    }

    private static CloseoutDto ToDto(PursuitCloseout row) => new()
    {
        PursuitId = row.PursuitId,
        OpportunityId = row.OpportunityId,
        Outcome = row.Outcome,
        ReasonCode = row.ReasonCode,
        Note = row.Note,
        UpdatedAt = row.UpdatedAt,
        CleatusSyncedAt = row.CleatusSyncedAt,
    };
}
