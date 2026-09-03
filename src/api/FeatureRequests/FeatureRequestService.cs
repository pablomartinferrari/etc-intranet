using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intranet.Api.FeatureRequests;

public sealed class FeatureRequestService(
    IntranetDbContext db,
    IFeatureRequestLlm llm,
    IFeatureRequestSmsClient? sms = null,
    ILogger<FeatureRequestService>? logger = null)
{
    private readonly ILogger<FeatureRequestService> _logger =
        logger ?? NullLogger<FeatureRequestService>.Instance;

    public async Task<FeatureRequestDto> CreateAsync(
        string page,
        string rawText,
        string createdBy,
        CancellationToken cancellationToken)
    {
        if (!FeatureRequestPages.IsValid(page))
        {
            throw new ArgumentException(
                "Area must be chat, lead, sales, general, opportunities, or pipeline.");
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

        var ticket = await StructureAsync(page, note, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var entity = new FeatureRequest
        {
            Page = page,
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
        await TryNotifyAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<FeatureRequestDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.FeatureRequests
            .AsNoTracking()
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FeatureRequestDto?> UpdateStatusAsync(
        int id,
        string status,
        CancellationToken cancellationToken)
    {
        if (!FeatureRequestStatuses.IsValid(status))
        {
            throw new ArgumentException("Status must be new, planned, or done.");
        }

        var entity = await db.FeatureRequests.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    private async Task TryNotifyAsync(FeatureRequestDto ticket, CancellationToken cancellationToken)
    {
        if (sms is not { IsConfigured: true } client)
        {
            _logger.LogDebug("Feature request SMS skipped; FeatureRequests__Sms is not configured.");
            return;
        }

        try
        {
            await client.SendAsync(FeatureRequestSmsMessage.Format(ticket), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Feature request SMS failed; ticket {Id} was still saved.",
                ticket.Id);
        }
    }

    private async Task<StructuredTicket> StructureAsync(
        string page,
        string rawText,
        CancellationToken cancellationToken)
    {
        var reply = await llm.ChatAsync(
            FeatureRequestStructurer.SystemPrompt,
            FeatureRequestStructurer.UserPrompt(page, rawText),
            cancellationToken);
        return FeatureRequestStructurer.TryParseLlmJson(reply)
            ?? FeatureRequestStructurer.FromFallback(page, rawText);
    }

    private static string Clip(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static FeatureRequestDto ToDto(FeatureRequest entity) => new()
    {
        Id = entity.Id,
        Page = entity.Page,
        CreatedBy = entity.CreatedBy,
        CreatedAt = entity.CreatedAt,
        RawText = entity.RawText,
        Title = entity.Title,
        Problem = entity.Problem,
        DesiredBehavior = entity.DesiredBehavior,
        DataInvolved = entity.DataInvolved,
        AcceptanceCriteria = entity.AcceptanceCriteria,
        Status = entity.Status,
        StructuredBy = entity.StructuredBy,
    };
}
