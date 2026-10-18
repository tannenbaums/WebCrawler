using System.Threading.Channels;
using Crawler.Core.Interfaces;
using Crawler.Core.Models;

namespace Crawler.Api;

public sealed class InProcessCrawlJobQueue : BackgroundService, ICrawlJobQueue
{
    private readonly Channel<(CrawlJobRequest Request, Guid JobId)> _jobs =
        Channel.CreateUnbounded<(CrawlJobRequest Request, Guid JobId)>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InProcessCrawlJobQueue> _logger;

    public InProcessCrawlJobQueue(IServiceScopeFactory scopeFactory, ILogger<InProcessCrawlJobQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ValueTask EnqueueAsync(CrawlJobRequest request, Guid jobId, CancellationToken cancellationToken = default) =>
        _jobs.Writer.WriteAsync((request, jobId), cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (request, jobId) in _jobs.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var executor = scope.ServiceProvider.GetRequiredService<ICrawlExecutionService>();
                await executor.ExecuteAsync(request, jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Queued crawl job {CrawlJobId} could not be processed", jobId);
            }
        }
    }
}