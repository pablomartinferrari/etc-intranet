using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Data.Entities;
using Intranet.Api.KnowledgeBase.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.KnowledgeBase.Controllers;

[ApiController]
[Route("api/kb/prompts")]
public sealed class KnowledgePromptsController : ControllerBase
{
    private readonly KnowledgeDbContext _db;

    public KnowledgePromptsController(KnowledgeDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PromptDto>>> List(
        [FromQuery] Guid? projectId,
        CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        var query = _db.Prompts.Where(p => p.UserOid == userOid);
        if (projectId.HasValue)
        {
            query = query.Where(p => p.ProjectId == projectId.Value || p.ProjectId == null);
        }

        var prompts = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new PromptDto(
                p.Id,
                p.ProjectId,
                p.Title,
                p.Content,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(prompts);
    }

    [HttpPost]
    public async Task<ActionResult<PromptDto>> Create(
        [FromBody] CreatePromptRequestDto request,
        CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Title and content are required.");
        }

        var now = DateTimeOffset.UtcNow;
        var prompt = new KbPrompt
        {
            Id = Guid.NewGuid(),
            UserOid = userOid,
            ProjectId = request.ProjectId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Prompts.Add(prompt);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = prompt.Id }, ToDto(prompt));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PromptDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        var prompt = await _db.Prompts
            .FirstOrDefaultAsync(p => p.Id == id && p.UserOid == userOid, cancellationToken);

        return prompt is null ? NotFound() : Ok(ToDto(prompt));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<PromptDto>> Update(
        Guid id,
        [FromBody] UpdatePromptRequestDto request,
        CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        var prompt = await _db.Prompts
            .FirstOrDefaultAsync(p => p.Id == id && p.UserOid == userOid, cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            prompt.Title = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            prompt.Content = request.Content.Trim();
        }

        prompt.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(prompt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        var prompt = await _db.Prompts
            .FirstOrDefaultAsync(p => p.Id == id && p.UserOid == userOid, cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        _db.Prompts.Remove(prompt);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private string? RequireUserOid() =>
        User.FindFirst("oid")?.Value
        ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

    private static PromptDto ToDto(KbPrompt prompt) =>
        new(prompt.Id, prompt.ProjectId, prompt.Title, prompt.Content, prompt.CreatedAt, prompt.UpdatedAt);
}
