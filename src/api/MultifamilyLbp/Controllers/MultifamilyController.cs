using Microsoft.AspNetCore.Mvc;
using Intranet.Api.MultifamilyLbp.Models;
using Intranet.Api.MultifamilyLbp.Services;

namespace Intranet.Api.MultifamilyLbp.Controllers;

[ApiController]
[Route("api/multifamily")]
public sealed class MultifamilyController : ControllerBase
{
    private readonly MultifamilyReadingsService _readings;
    private readonly ILogger<MultifamilyController> _logger;

    public MultifamilyController(MultifamilyReadingsService readings, ILogger<MultifamilyController> logger)
    {
        _readings = readings;
        _logger = logger;
    }

    /// <summary>All merged shots for the job in the Units dataset (SharePoint AreaType = Units).</summary>
    [HttpGet("{jobNumber}/units")]
    [ProducesResponseType(typeof(IReadOnlyList<XrfReadingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<XrfReadingDto>>> GetUnits(string jobNumber, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _readings.GetReadingsAsync(jobNumber, "Units", cancellationToken);
            return Ok(data);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Units readings failed for job {Job}", jobNumber);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving units readings for job {Job}", jobNumber);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred." });
        }
    }

    /// <summary>All merged shots for the job in the Common Areas dataset (SharePoint AreaType = Common Areas).</summary>
    [HttpGet("{jobNumber}/common-areas")]
    [ProducesResponseType(typeof(IReadOnlyList<XrfReadingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<XrfReadingDto>>> GetCommonAreas(string jobNumber, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _readings.GetReadingsAsync(jobNumber, "Common Areas", cancellationToken);
            return Ok(data);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Common areas readings failed for job {Job}", jobNumber);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }
}
