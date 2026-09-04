using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Data.Entities;
using Intranet.Api.KnowledgeBase.Models;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.KnowledgeBase.Services;

public interface IKbProjectAccessService
{
    Task<KbProjectAccessResult?> GetAccessAsync(Guid projectId, string userOid, CancellationToken cancellationToken);

    Task<KbProjectAccessResult> RequireAsync(
        Guid projectId,
        string userOid,
        KbProjectPermission permission,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectListItem>> ListAccessibleAsync(string userOid, CancellationToken cancellationToken);
}

public sealed record ProjectListItem(KbProject Project, KbProjectAccessResult Access);

public sealed class KbProjectAccessService : IKbProjectAccessService
{
    private readonly KnowledgeDbContext _db;
    private readonly IGraphDirectoryClient _graph;
    private readonly ILogger<KbProjectAccessService> _logger;

    public KbProjectAccessService(
        KnowledgeDbContext db,
        IGraphDirectoryClient graph,
        ILogger<KbProjectAccessService> logger)
    {
        _db = db;
        _graph = graph;
        _logger = logger;
    }

    public async Task<KbProjectAccessResult?> GetAccessAsync(
        Guid projectId,
        string userOid,
        CancellationToken cancellationToken)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var shares = await _db.ProjectShares
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .Select(s => new KbProjectShareHint(s.PrincipalType, s.PrincipalOid, s.Role))
            .ToListAsync(cancellationToken);

        var groupOids = shares
            .Where(s => string.Equals(s.PrincipalType, KbProjectRoles.Group, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.PrincipalOid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var memberGroups = await _graph.CheckMemberGroupsAsync(userOid, groupOids, cancellationToken);
        var role = KbProjectAccess.ResolveRole(userOid, project.UserOid, shares, memberGroups);
        if (role is null)
        {
            return null;
        }

        return new KbProjectAccessResult(project.Id, project.UserOid, role, shares.Count > 0);
    }

    public async Task<KbProjectAccessResult> RequireAsync(
        Guid projectId,
        string userOid,
        KbProjectPermission permission,
        CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync(projectId, userOid, cancellationToken);
        if (access is null)
        {
            throw KbProjectAccessException.NotFound();
        }

        if (!access.Allows(permission))
        {
            throw KbProjectAccessException.Forbidden();
        }

        return access;
    }

    public async Task<IReadOnlyList<ProjectListItem>> ListAccessibleAsync(
        string userOid,
        CancellationToken cancellationToken)
    {
        var owned = await _db.Projects
            .AsNoTracking()
            .Where(p => p.UserOid == userOid)
            .ToListAsync(cancellationToken);

        var userOidLower = userOid.ToLowerInvariant();
        var userShares = await _db.ProjectShares
            .AsNoTracking()
            .Where(s => s.PrincipalType == KbProjectRoles.User && s.PrincipalOid.ToLower() == userOidLower)
            .ToListAsync(cancellationToken);

        var groupShares = await _db.ProjectShares
            .AsNoTracking()
            .Where(s => s.PrincipalType == KbProjectRoles.Group)
            .ToListAsync(cancellationToken);

        var groupOids = groupShares
            .Select(s => s.PrincipalOid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlySet<string> memberGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (groupOids.Count > 0)
        {
            memberGroups = await _graph.CheckMemberGroupsAsync(userOid, groupOids, cancellationToken);
            if (groupOids.Count > 0 && memberGroups.Count == 0 && !_graph.IsConfigured)
            {
                _logger.LogInformation("Graph is not configured; group-shared projects are hidden from the list.");
            }
        }

        var matchedGroupShares = groupShares
            .Where(s => memberGroups.Contains(s.PrincipalOid))
            .ToList();

        var extraProjectIds = userShares.Select(s => s.ProjectId)
            .Concat(matchedGroupShares.Select(s => s.ProjectId))
            .Distinct()
            .Except(owned.Select(p => p.Id))
            .ToList();

        var extraProjects = extraProjectIds.Count == 0
            ? []
            : await _db.Projects
                .AsNoTracking()
                .Where(p => extraProjectIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

        var projects = owned.Concat(extraProjects).ToList();
        if (projects.Count == 0)
        {
            return [];
        }

        var projectIds = projects.Select(p => p.Id).ToList();
        var allShares = await _db.ProjectShares
            .AsNoTracking()
            .Where(s => projectIds.Contains(s.ProjectId))
            .ToListAsync(cancellationToken);

        var sharesByProject = allShares
            .GroupBy(s => s.ProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ProjectListItem>(projects.Count);
        foreach (var project in projects.OrderByDescending(p => p.UpdatedAt))
        {
            sharesByProject.TryGetValue(project.Id, out var projectShares);
            projectShares ??= [];
            var hints = projectShares
                .Select(s => new KbProjectShareHint(s.PrincipalType, s.PrincipalOid, s.Role))
                .ToList();
            var role = KbProjectAccess.ResolveRole(userOid, project.UserOid, hints, memberGroups)
                ?? KbProjectRoles.Viewer;
            result.Add(new ProjectListItem(
                project,
                new KbProjectAccessResult(project.Id, project.UserOid, role, projectShares.Count > 0)));
        }

        return result;
    }

    public static ProjectDto ToDto(KbProject project, KbProjectAccessResult access) =>
        new(
            project.Id,
            project.Name,
            project.Description,
            project.Instructions,
            project.CreatedAt,
            project.UpdatedAt,
            project.Area,
            access.Role,
            access.IsShared);

    public static ProjectDto ToOwnerDto(KbProject project, bool isShared = false) =>
        new(
            project.Id,
            project.Name,
            project.Description,
            project.Instructions,
            project.CreatedAt,
            project.UpdatedAt,
            project.Area,
            KbProjectRoles.Owner,
            isShared);
}
