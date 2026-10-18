using Crawler.Core.Models;

namespace Crawler.Core.Interfaces;

public interface IRabbitMqPublisher
{
    Task PublishCrawlRequestAsync(CrawlJobRequest request, Guid jobId, CancellationToken cancellationToken = default);
}
