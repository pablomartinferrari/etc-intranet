namespace Intranet.Api.KnowledgeBase.Services;

public interface IOllamaHealthProbe
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Forget a cached "up" result after a generation failure.</summary>
    void Invalidate();
}
