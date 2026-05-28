using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Intranet.Api.MultifamilyLbp.Config;
using Intranet.Api.MultifamilyLbp.Data;
using Intranet.Api.MultifamilyLbp.Services;

namespace Intranet.Api.MultifamilyLbp.Controllers;

[ApiController]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobsController> _logger;
    private readonly MultifamilyDbContext _db;
    private readonly JobEntityService _jobEntityService;

    public JobsController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<JobsController> logger,
        MultifamilyDbContext db,
        JobEntityService jobEntityService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _db = db;
        _jobEntityService = jobEntityService;
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<RecentJobDto>>> Recent([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var jobs = await _db.Jobs
            .OrderByDescending(j => j.UpdatedAt)
            .Take(limit)
            .Select(j => new RecentJobDto(j.JobIdentifier, j.ClientName, j.FacilityName, j.UpdatedAt))
            .ToListAsync(ct);
        return Ok(jobs);
    }

    [HttpPost("{jobNumber}/ensure")]
    public async Task<ActionResult<object>> Ensure(string jobNumber, CancellationToken ct)
    {
        var job = await _jobEntityService.GetJobAsync(jobNumber, ct);
        if (job == null)
        {
            await _jobEntityService.ResolveAsync(jobNumber, EntityRegistry.MultifamilyLbp, ct);
            job = await _jobEntityService.GetJobAsync(jobNumber, ct);
        }
        return Ok(new { jobIdentifier = job!.JobIdentifier, created = true });
    }

    [HttpGet("{jobNumber}/entities")]
    public async Task<ActionResult<IReadOnlyList<EntityListItemDto>>> ListEntities(string jobNumber, CancellationToken ct)
    {
        var job = await _jobEntityService.GetJobAsync(jobNumber, ct);
        var registered = job == null
            ? new List<string>()
            : await _db.JobEntities.Where(e => e.JobId == job.Id).Select(e => e.EntitySlug).ToListAsync(ct);

        var items = EntityRegistry.List().Select(def => new EntityListItemDto(
            def.Slug,
            def.DisplayName,
            def.Description,
            registered.Contains(def.Slug)));
        return Ok(items);
    }

    /// <summary>
    /// Proxies GET /jobs/{id} from the ETC Jobs API and returns a normalized DTO for the React app.
    /// </summary>
    [HttpGet("{jobNumber}")]
    [ProducesResponseType(typeof(JobApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobApiResponse>> GetJob(string jobNumber, CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["EtcJobsApi:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.LogWarning("EtcJobsApi:BaseUrl is not configured");
            return NotFound();
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var url = $"{baseUrl}/jobs/{Uri.EscapeDataString(jobNumber)}";

        using var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return NotFound();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Jobs API returned {Status}", response.StatusCode);
            return NotFound();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var resolvedJobId = root.TryGetProperty("job_id", out var jid) ? jid.GetInt32() : 0;
        if (resolvedJobId == 0 && int.TryParse(jobNumber, out var pn))
            resolvedJobId = pn;

        var clientName = root.TryGetProperty("client_name", out var cn) ? cn.GetString() : null;
        var facilityName = root.TryGetProperty("facility_name", out var fn) ? fn.GetString() : null;
        var addr = root.TryGetProperty("FacilityAddress", out var fa) ? fa.GetString() : null;
        var city = root.TryGetProperty("FacilityCity", out var fc) ? fc.GetString() : null;
        var state = root.TryGetProperty("FacilityState", out var fs) ? fs.GetString() : null;
        var zip = root.TryGetProperty("FacilityZip", out var fz) ? fz.GetString() : null;
        var parts = new[] { addr, city, state, zip }.Where(s => !string.IsNullOrWhiteSpace(s));
        var facilityAddress = string.Join(", ", parts);

        var status = root.TryGetProperty("job_status", out var st) ? st.GetInt32().ToString() : null;

        var jobResponse = new JobApiResponse(
            resolvedJobId,
            clientName ?? "",
            facilityName,
            string.IsNullOrEmpty(facilityAddress) ? null : facilityAddress,
            status);

        await _jobEntityService.UpdateJobMetadataAsync(
            jobNumber,
            clientName,
            facilityName,
            string.IsNullOrEmpty(facilityAddress) ? null : facilityAddress,
            status,
            cancellationToken);

        return Ok(jobResponse);
    }
}

public record RecentJobDto(string JobIdentifier, string? ClientName, string? FacilityName, DateTimeOffset UpdatedAt);
public record EntityListItemDto(string Slug, string DisplayName, string Description, bool HasData);

public record JobApiResponse(
    int JobId,
    string ClientName,
    string? FacilityName,
    string? FacilityAddress,
    string? JobStatus);
