using Intranet.Api.KnowledgeBase.Models;
using Microsoft.AspNetCore.Mvc;

namespace Intranet.Api.KnowledgeBase.AgentSources;

[ApiController]
[Route("api/kb/sources")]
public sealed class AgentSourcesController : ControllerBase
{
    private readonly AgentSourceService _sources;

    public AgentSourcesController(AgentSourceService sources)
    {
        _sources = sources;
    }

    [HttpGet("capabilities")]
    public ActionResult<AgentSourceCapabilitiesDto> Capabilities() =>
        Ok(_sources.Capabilities());

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentSourceDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _sources.ListAsync(cancellationToken));

    [HttpPost("probe")]
    public async Task<ActionResult<AgentSourceProbeDto>> Probe(
        [FromBody] AgentSourceProbeRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sources.ProbeAsync(request.SiteUrl, request.FolderPath, cancellationToken));
        }
        catch (AgentSourceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<AgentSourceDto>> Connect(
        [FromBody] AgentSourceConnectRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userOid = User.FindFirst("oid")?.Value
                ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                ?? "unknown";
            var createdBy = User.FindFirst("preferred_username")?.Value
                ?? User.Identity?.Name
                ?? userOid;
            var created = await _sources.ConnectAsync(request, userOid, createdBy, cancellationToken);
            return StatusCode(created.Status == "awaiting_approval" ? 202 : 201, created);
        }
        catch (AgentSourceConfirmRequiredException ex)
        {
            return StatusCode(409, new { message = ex.Message, code = "confirmRequired", probe = ex.Probe });
        }
        catch (AgentSourceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Disconnect(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _sources.DisconnectAsync(id, cancellationToken);
            return NoContent();
        }
        catch (AgentSourceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<ActionResult<AgentSourceJobDto>> GetJob(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _sources.GetJobAsync(jobId, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }
}
