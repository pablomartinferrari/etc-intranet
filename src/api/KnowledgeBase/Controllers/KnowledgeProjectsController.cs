using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Data.Entities;
using Intranet.Api.KnowledgeBase.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.KnowledgeBase.Controllers;

[ApiController]
[Route("api/kb/projects")]
public sealed class KnowledgeProjectsController : ControllerBase
{
    private readonly KnowledgeDbContext _db;

    public KnowledgeProjectsController(KnowledgeDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> List(CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        var projects = await _db.Projects
            .Where(p => p.UserOid == userOid)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new ProjectDto(
                p.Id,
                p.Name,
                p.Description,
                p.Instructions,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(projects);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(
        [FromBody] CreateProjectRequestDto request,
        CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var project = new KbProject
        {
            Id = Guid.NewGuid(),
            UserOid = userOid,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Instructions = request.Instructions?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = project.Id }, ToDto(project));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.UserOid == userOid, cancellationToken);

        return project is null ? NotFound() : Ok(ToDto(project));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(
        Guid id,
        [FromBody] UpdateProjectRequestDto request,
        CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.UserOid == userOid, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            project.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            project.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
        }

        if (request.Instructions is not null)
        {
            project.Instructions = string.IsNullOrWhiteSpace(request.Instructions)
                ? null
                : request.Instructions.Trim();
        }

        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(project));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userOid = RequireUserOid();
        if (userOid is null)
        {
            return Unauthorized();
        }

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.UserOid == userOid, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private string? RequireUserOid() =>
        User.FindFirst("oid")?.Value
        ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

    private static ProjectDto ToDto(KbProject project) =>
        new(project.Id, project.Name, project.Description, project.Instructions, project.CreatedAt, project.UpdatedAt);
}
