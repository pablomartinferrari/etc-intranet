namespace Intranet.Api.KnowledgeBase.AgentSources;

public sealed class AgentSourceIngestWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AgentSourceIngestWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Agent source ingest worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IAgentSourceIngestRunner>();
                processed = await runner.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Agent source ingest worker loop failed.");
            }

            var delay = processed ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(4);
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
