using Intranet.Api.KnowledgeBase.Options;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.Services;

/// <summary>
/// Short-timeout probe of the Ollama HTTP endpoint. A deallocated GPU VM does not
/// refuse TCP quickly; the typed <see cref="OllamaClient"/> HttpClient uses the
/// framework default of 100 seconds, which is why Chat hung with an empty UI.
/// This probe cancels in a couple of seconds and caches a "down" result briefly
/// so every chat request does not pay the probe cost.
/// </summary>
public sealed class OllamaHealthProbe : IOllamaHealthProbe
{
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan DownCacheTtl = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan UpCacheTtl = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly KnowledgeBaseOptions _options;
    private readonly ILogger<OllamaHealthProbe> _logger;
    private readonly object _gate = new();
    private bool _hasCache;
    private bool _cachedHealthy;
    private DateTimeOffset _cachedUntil;

    public OllamaHealthProbe(
        HttpClient http,
        IOptions<KnowledgeBaseOptions> options,
        ILogger<OllamaHealthProbe> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_hasCache && DateTimeOffset.UtcNow < _cachedUntil)
            {
                return _cachedHealthy;
            }
        }

        var healthy = await ProbeAsync(cancellationToken);
        var ttl = healthy ? UpCacheTtl : DownCacheTtl;
        lock (_gate)
        {
            _hasCache = true;
            _cachedHealthy = healthy;
            _cachedUntil = DateTimeOffset.UtcNow.Add(ttl);
        }

        return healthy;
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _hasCache = false;
        }
    }

    private async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.OllamaBaseUrl)
            ? "http://localhost:11434"
            : _options.OllamaBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/tags";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ProbeTimeout);
            using var response = await _http.GetAsync(url, cts.Token);
            var ok = response.IsSuccessStatusCode;
            if (!ok)
            {
                _logger.LogInformation(
                    "Ollama health probe failed: {Status} from {Url}",
                    (int)response.StatusCode,
                    url);
            }

            return ok;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogInformation(ex, "Ollama health probe could not reach {Url} within {Timeout}.", url, ProbeTimeout);
            return false;
        }
    }
}
