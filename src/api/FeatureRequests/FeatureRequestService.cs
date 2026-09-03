using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Intranet.Api.FeatureRequests;

public sealed class FeatureRequestService(
    IntranetDbContext db,
    IFeatureRequestLlm llm,
    IFeatureRequestSmsClient? sms = null,
    IFeatureRequestEmailClient? email = null,
    IOptions<FeatureRequestOptions>? options = null,
    IHostEnvironment? environment = null,
    ILogger<FeatureRequestService>? logger = null)
{
    private readonly ILogger<FeatureRequestService> _logger =
        logger ?? NullLogger<FeatureRequestService>.Instance;
    private readonly FeatureRequestOptions _options = options?.Value ?? new FeatureRequestOptions();
    private readonly bool _isProduction = environment?.IsProduction() == true;

    public async Task<FeatureRequestDto> CreateAsync(
        string page,
        string rawText,
        string createdBy,
        CancellationToken cancellationToken,
        string? areaLabel = null)
    {
        if (!FeatureRequestPages.IsValid(page))
        {
            throw new ArgumentException(
                "Area must be chat, lead, sales, general, other, opportunities, or pipeline.");
        }

        var note = rawText.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("Write a short note about the change you want.");
        }

        if (note.Length > 8000)
        {
            throw new ArgumentException("Keep the note under 8,000 characters.");
        }

        var resolvedAreaLabel = ResolveAreaLabel(page, areaLabel);
        var ticket = await StructureAsync(page, note, resolvedAreaLabel, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var entity = new FeatureRequest
        {
            Page = page,
            AreaLabel = resolvedAreaLabel,
            CreatedBy = Clip(string.IsNullOrWhiteSpace(createdBy) ? "unknown" : createdBy.Trim(), 320),
            CreatedAt = now,
            RawText = note,
            Title = Clip(ticket.Title, 200),
            Problem = Clip(ticket.Problem, 4000),
            DesiredBehavior = Clip(ticket.DesiredBehavior, 4000),
            DataInvolved = Clip(ticket.DataInvolved, 4000),
            AcceptanceCriteria = Clip(ticket.AcceptanceCriteria, 4000),
            Status = FeatureRequestStatuses.New,
            StructuredBy = Clip(ticket.StructuredBy, 32),
        };

        db.FeatureRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        var dto = ToDto(entity);
        await TryNotifySmsAsync(dto, FeatureRequestSmsMessage.Format(dto), cancellationToken);
        await TryNotifyApproversAsync(dto, cancellationToken);
        return dto;
    }

    public Task<IReadOnlyList<FeatureRequestDto>> ListAsync(CancellationToken cancellationToken) =>
        ListAsync(null, cancellationToken);

    public async Task<IReadOnlyList<FeatureRequestDto>> ListAsync(
        FeatureRequestActor? actor,
        CancellationToken cancellationToken)
    {
        var rows = await db.FeatureRequests
            .AsNoTracking()
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .ToListAsync(cancellationToken);
        var viewer = actor ?? FeatureRequestActor.Anonymous;
        return rows.Select(row => ToDto(row, viewer)).ToList();
    }

    public FeatureRequestMetaDto GetMeta(FeatureRequestActor actor)
    {
        var approvers = _options.GetApproverEmails();
        return new FeatureRequestMetaDto
        {
            ApproverEmailsConfigured = approvers.Count > 0,
            ViewerCanApprove = FeatureRequestAuthorization.CanApproveOrReject(
                actor,
                approvers,
                _isProduction,
                out _,
                out _),
            ApproverCount = approvers.Count,
        };
    }

    public Task<FeatureRequestDto?> UpdateStatusAsync(
        int id,
        string status,
        CancellationToken cancellationToken) =>
        UpdateStatusAsync(id, status, FeatureRequestActor.Anonymous, cancellationToken);

    public async Task<FeatureRequestDto?> UpdateStatusAsync(
        int id,
        string status,
        FeatureRequestActor actor,
        CancellationToken cancellationToken)
    {
        var next = FeatureRequestStatuses.Normalize(status);
        if (!FeatureRequestStatuses.IsValid(status))
        {
            throw FeatureRequestException.BadRequest(
                "invalid_status",
                "Status must be new, approved, rejected, shipped, or closed.");
        }

        var entity = await db.FeatureRequests.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var current = FeatureRequestStatuses.Normalize(entity.Status);
        var approvers = _options.GetApproverEmails();
        if (!FeatureRequestAuthorization.CanChangeStatus(
                current,
                next,
                actor,
                entity.CreatedBy,
                approvers,
                _isProduction,
                out var error,
                out var message))
        {
            throw error is "invalid_status" or "invalid_transition"
                ? FeatureRequestException.BadRequest(error, message)
                : FeatureRequestException.Forbidden(error, message);
        }

        if (approvers.Count == 0
            && !_isProduction
            && actor.IsAuthenticated
            && next is FeatureRequestStatuses.Approved or FeatureRequestStatuses.Rejected
            && current == FeatureRequestStatuses.New)
        {
            _logger.LogWarning(
                "FeatureRequests__ApproverEmails is empty; allowing {Actor} to {Action} request {Id} in {Environment}.",
                actor.Display,
                next,
                entity.Id,
                environment?.EnvironmentName ?? "Development");
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = next;
        if (next is FeatureRequestStatuses.Approved or FeatureRequestStatuses.Rejected)
        {
            entity.ReviewedBy = Clip(actor.Display, 320);
            entity.ReviewedAt = now;
        }

        if (next == FeatureRequestStatuses.Closed)
        {
            entity.ClosedBy = Clip(actor.Display, 320);
            entity.ClosedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        var dto = ToDto(entity, actor);
        if (next == FeatureRequestStatuses.Approved)
        {
            await TryNotifySmsAsync(dto, FeatureRequestSmsMessage.FormatApproved(dto), cancellationToken);
        }

        return dto;
    }

    private async Task TryNotifySmsAsync(
        FeatureRequestDto ticket,
        string body,
        CancellationToken cancellationToken)
    {
        if (sms is not { IsConfigured: true } client)
        {
            _logger.LogDebug("Feature request SMS skipped; FeatureRequests__Sms is not configured.");
            return;
        }

        try
        {
            await client.SendAsync(body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Feature request SMS failed; ticket {Id} was still saved.",
                ticket.Id);
        }
    }

    private async Task TryNotifyApproversAsync(FeatureRequestDto ticket, CancellationToken cancellationToken)
    {
        var approvers = _options.GetApproverEmails();
        if (approvers.Count == 0)
        {
            _logger.LogDebug("Feature request email skipped; FeatureRequests__ApproverEmails is empty.");
            return;
        }

        if (email is not { IsConfigured: true } client)
        {
            _logger.LogDebug("Feature request email skipped; FeatureRequests__Email is not configured.");
            return;
        }

        try
        {
            var (subject, text, html) = FeatureRequestEmailMessage.FormatNew(
                ticket,
                _options.ResolvedPublicBaseUrl);
            await client.SendAsync(approvers, subject, text, html, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Feature request email failed; ticket {Id} was still saved.",
                ticket.Id);
        }
    }

    private async Task<StructuredTicket> StructureAsync(
        string page,
        string rawText,
        string? areaLabel,
        CancellationToken cancellationToken)
    {
        var reply = await llm.ChatAsync(
            FeatureRequestStructurer.SystemPrompt,
            FeatureRequestStructurer.UserPrompt(page, rawText, areaLabel),
            cancellationToken);
        return FeatureRequestStructurer.TryParseLlmJson(reply)
            ?? FeatureRequestStructurer.FromFallback(page, rawText, areaLabel);
    }

    internal static string? ResolveAreaLabel(string page, string? areaLabel)
    {
        if (!FeatureRequestPages.IsOther(page))
        {
            return null;
        }

        var label = areaLabel?.Trim();
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Name the area or topic this request is about.");
        }

        if (label.Length > FeatureRequestPages.AreaLabelMaxLength)
        {
            throw new ArgumentException(
                $"Keep the area name under {FeatureRequestPages.AreaLabelMaxLength} characters.");
        }

        return label;
    }

    private static string Clip(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private FeatureRequestDto ToDto(FeatureRequest entity, FeatureRequestActor? actor = null)
    {
        var viewer = actor ?? FeatureRequestActor.Anonymous;
        var approvers = _options.GetApproverEmails();
        return new FeatureRequestDto
        {
            Id = entity.Id,
            Page = entity.Page,
            AreaLabel = entity.AreaLabel,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            RawText = entity.RawText,
            Title = entity.Title,
            Problem = entity.Problem,
            DesiredBehavior = entity.DesiredBehavior,
            DataInvolved = entity.DataInvolved,
            AcceptanceCriteria = entity.AcceptanceCriteria,
            Status = FeatureRequestStatuses.Normalize(entity.Status),
            StructuredBy = entity.StructuredBy,
            ReviewedBy = entity.ReviewedBy,
            ReviewedAt = entity.ReviewedAt,
            ClosedBy = entity.ClosedBy,
            ClosedAt = entity.ClosedAt,
            ViewerCanApprove = FeatureRequestAuthorization.CanApproveOrReject(
                viewer,
                approvers,
                _isProduction,
                out _,
                out _),
            ViewerCanClose = FeatureRequestAuthorization.CanClose(
                viewer,
                entity.CreatedBy,
                approvers,
                _isProduction,
                out _,
                out _),
        };
    }
}
