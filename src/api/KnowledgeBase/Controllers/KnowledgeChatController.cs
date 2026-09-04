using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.KnowledgeBase.Controllers;

[ApiController]
[Route("api/kb")]
public sealed class KnowledgeChatController : ControllerBase
{
    private readonly RagService _rag;
    private readonly ChatExportService _export;
    private readonly KnowledgeDbContext _db;
    private readonly IKbProjectAccessService _access;
    private readonly ILogger<KnowledgeChatController> _logger;

    public KnowledgeChatController(
        RagService rag,
        ChatExportService export,
        KnowledgeDbContext db,
        IKbProjectAccessService access,
        ILogger<KnowledgeChatController> logger)
    {
        _rag = rag;
        _export = export;
        _db = db;
        _access = access;
        _logger = logger;
    }

    /// <summary>Feature flags only (no secrets). Anonymous so devs can verify WebSearch config without a token.</summary>
    [AllowAnonymous]
    [HttpGet("chat/capabilities")]
    public ActionResult<ChatCapabilitiesDto> GetCapabilities() => Ok(_rag.GetCapabilities());

    [HttpGet("chat/sessions")]
    public async Task<ActionResult<IReadOnlyList<ChatSessionDto>>> ListSessions(
        [FromQuery] Guid? projectId,
        CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        if (projectId.HasValue)
        {
            try
            {
                await _access.RequireAsync(projectId.Value, userOid, KbProjectPermission.View, cancellationToken);
            }
            catch (KbProjectAccessException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        var query = _db.ChatSessions.Where(s => s.UserOid == userOid);
        if (projectId.HasValue)
        {
            query = query.Where(s => s.ProjectId == projectId.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.UpdatedAt)
            .Take(50)
            .Select(s => new ChatSessionDto(
                s.Id,
                s.ProjectId,
                s.Title,
                s.CreatedAt,
                s.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(sessions);
    }

    [HttpPatch("chat/sessions/{sessionId:guid}")]
    public async Task<ActionResult<ChatSessionDto>> UpdateSession(
        Guid sessionId,
        [FromBody] UpdateChatSessionRequestDto request,
        CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Title is required.");
        }

        var session = await _db.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserOid == userOid, cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        session.Title = request.Title.Trim();
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ChatSessionDto(
            session.Id,
            session.ProjectId,
            session.Title,
            session.CreatedAt,
            session.UpdatedAt));
    }

    [HttpGet("chat/sessions/{sessionId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetSessionMessages(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        var ownsSession = await _db.ChatSessions
            .AnyAsync(s => s.Id == sessionId && s.UserOid == userOid, cancellationToken);

        if (!ownsSession)
        {
            return NotFound();
        }

        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var messageIds = messages.Select(m => m.Id).ToList();
        var attachmentsByMessage = await _db.GeneratedFiles
            .AsNoTracking()
            .Where(f => f.MessageId != null && messageIds.Contains(f.MessageId.Value))
            .GroupBy(f => f.MessageId!.Value)
            .ToDictionaryAsync(
                g => g.Key,
                g => (IReadOnlyList<ChatAttachmentDto>)g
                    .Select(f => new ChatAttachmentDto(f.Id, f.Filename, f.MimeType, f.Format))
                    .ToList(),
                cancellationToken);

        var result = messages.Select(m =>
        {
            var (citations, generation) = RagService.ParseMessagePayload(m.CitationsJson);
            attachmentsByMessage.TryGetValue(m.Id, out var attachments);

            return new ChatMessageDto(
                m.Id,
                m.Role,
                m.Content,
                citations,
                attachments is { Count: > 0 } ? attachments : null,
                m.CreatedAt,
                generation);
        }).ToList();

        return Ok(result);
    }

    [HttpGet("generated/{fileId:guid}/download")]
    public async Task<IActionResult> DownloadGeneratedFile(
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        var result = await _export.TryReadForUserAsync(fileId, userOid, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        var (file, bytes) = result.Value;
        return File(bytes, file.MimeType, file.Filename);
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponseDto>> Chat(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query is required.");
        }

        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        if (request.ProjectId.HasValue)
        {
            try
            {
                await _access.RequireAsync(request.ProjectId.Value, userOid, KbProjectPermission.View, cancellationToken);
            }
            catch (KbProjectAccessException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        try
        {
            var response = await _rag.ChatAsync(request, userOid, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "KB chat request failed.");
            var message = ex is ChatUnavailableException
                ? ex.Message
                : ChatUnavailableException.UserMessage;
            return Ok(new ChatResponseDto(Guid.Empty, message, [], "none", []));
        }
    }

}
