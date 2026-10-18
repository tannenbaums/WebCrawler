namespace Crawler.Worker;

public class CrawlWorker : BackgroundService
{
    private readonly ILogger<CrawlWorker> _logger;

    public CrawlWorker(ILogger<CrawlWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Crawler worker is running at {Time}", DateTimeOffset.Now);
            await Task.Delay(5000, stoppingToken);
        }
    }
}
