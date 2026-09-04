using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Data.Entities;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.KnowledgeBase.Controllers;

[ApiController]
[Route("api/kb/projects")]
public sealed class KnowledgeProjectsController : ControllerBase
{
    private readonly KnowledgeDbContext _db;
    private readonly IKbProjectAccessService _access;
    private readonly IGraphDirectoryClient _directory;

    public KnowledgeProjectsController(
        KnowledgeDbContext db,
        IKbProjectAccessService access,
        IGraphDirectoryClient directory)
    {
        _db = db;
        _access = access;
        _directory = directory;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> List(CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        var projects = await _access.ListAccessibleAsync(userOid, cancellationToken);
        return Ok(projects.Select(p => KbProjectAccessService.ToDto(p.Project, p.Access)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(
        [FromBody] CreateProjectRequestDto request,
        CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
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
            Area = KbProjectFields.NormalizeArea(request.Area),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = project.Id }, KbProjectAccessService.ToOwnerDto(project));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        try
        {
            var access = await _access.RequireAsync(id, userOid, KbProjectPermission.View, cancellationToken);
            var project = await _db.Projects.AsNoTracking().FirstAsync(p => p.Id == id, cancellationToken);
            return Ok(KbProjectAccessService.ToDto(project, access));
        }
        catch (KbProjectAccessException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(
        Guid id,
        [FromBody] UpdateProjectRequestDto request,
        CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        try
        {
            var access = await _access.RequireAsync(id, userOid, KbProjectPermission.Manage, cancellationToken);
            var project = await _db.Projects.FirstAsync(p => p.Id == id, cancellationToken);

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

            if (request.Area is not null)
            {
                project.Area = KbProjectFields.NormalizeArea(request.Area);
            }

            project.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(KbProjectAccessService.ToDto(project, access with { IsShared = access.IsShared }));
        }
        catch (KbProjectAccessException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        try
        {
            await _access.RequireAsync(id, userOid, KbProjectPermission.Manage, cancellationToken);
        }
        catch (KbProjectAccessException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/shares")]
    public async Task<ActionResult<IReadOnlyList<ProjectShareDto>>> ListShares(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        try
        {
            await _access.RequireAsync(id, userOid, KbProjectPermission.Manage, cancellationToken);
        }
        catch (KbProjectAccessException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }

        var shares = await _db.ProjectShares
            .AsNoTracking()
            .Where(s => s.ProjectId == id)
            .OrderBy(s => s.PrincipalDisplayName)
            .Select(s => ToShareDto(s))
            .ToListAsync(cancellationToken);

        return Ok(shares);
    }

    [HttpPost("{id:guid}/shares")]
    public async Task<ActionResult<ProjectShareDto>> CreateShare(
        Guid id,
        [FromBody] CreateProjectShareRequestDto request,
        CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        try
        {
            var access = await _access.RequireAsync(id, userOid, KbProjectPermission.Manage, cancellationToken);

            if (!KbProjectRoles.IsPrincipalType(request.PrincipalType)
                || string.IsNullOrWhiteSpace(request.PrincipalOid)
                || !KbProjectRoles.IsShareRole(request.Role))
            {
                return BadRequest("principalType must be user or group, role must be viewer or editor, and principalOid is required.");
            }

            var principalType = KbProjectRoles.NormalizePrincipalType(request.PrincipalType);
            var principalOid = request.PrincipalOid.Trim();
            var role = KbProjectRoles.NormalizeRole(request.Role);

            if (KbProjectAccess.OidsEqual(principalOid, access.OwnerOid)
                && principalType == KbProjectRoles.User)
            {
                return BadRequest("The project owner already has full access.");
            }

            var exists = await _db.ProjectShares.AnyAsync(
                s => s.ProjectId == id && s.PrincipalType == principalType && s.PrincipalOid == principalOid,
                cancellationToken);
            if (exists)
            {
                return Conflict("That user or group already has access to this project.");
            }

            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? principalOid
                : request.DisplayName.Trim();
            var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

            var share = new KbProjectShare
            {
                Id = Guid.NewGuid(),
                ProjectId = id,
                PrincipalType = principalType,
                PrincipalOid = principalOid,
                PrincipalDisplayName = displayName,
                PrincipalEmail = email,
                Role = role,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByOid = userOid,
            };

            _db.ProjectShares.Add(share);

            var project = await _db.Projects.FirstAsync(p => p.Id == id, cancellationToken);
            project.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(ListShares), new { id }, ToShareDto(share));
        }
        catch (KbProjectAccessException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }

    [HttpDelete("{id:guid}/shares/{shareId:guid}")]
    public async Task<IActionResult> DeleteShare(Guid id, Guid shareId, CancellationToken cancellationToken)
    {
        var userOid = KnowledgeUser.GetOid(User);
        if (userOid is null)
        {
            return Unauthorized();
        }

        try
        {
            await _access.RequireAsync(id, userOid, KbProjectPermission.Manage, cancellationToken);
        }
        catch (KbProjectAccessException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }

        var share = await _db.ProjectShares
            .FirstOrDefaultAsync(s => s.Id == shareId && s.ProjectId == id, cancellationToken);
        if (share is null)
        {
            return NotFound();
        }

        _db.ProjectShares.Remove(share);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ProjectShareDto ToShareDto(KbProjectShare share) =>
        new(
            share.Id,
            share.PrincipalType,
            share.PrincipalOid,
            share.PrincipalDisplayName,
            share.PrincipalEmail,
            share.Role,
            share.CreatedAt);
}

[ApiController]
[Route("api/kb/directory")]
public sealed class KnowledgeDirectoryController : ControllerBase
{
    private readonly IGraphDirectoryClient _directory;

    public KnowledgeDirectoryController(IGraphDirectoryClient directory)
    {
        _directory = directory;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<DirectoryPrincipalDto>>> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        if (KnowledgeUser.GetOid(User) is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<DirectoryPrincipalDto>());
        }

        try
        {
            var results = await _directory.SearchAsync(q, cancellationToken);
            return Ok(results);
        }
        catch (GraphDirectoryException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
    }
}
