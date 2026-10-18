using Crawler.Core.Models;

namespace Crawler.Core.Interfaces;

public interface ICrawlJobQueue
{
    ValueTask EnqueueAsync(CrawlJobRequest request, Guid jobId, CancellationToken cancellationToken = default);
}