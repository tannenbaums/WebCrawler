using Crawler.Core.Models;

namespace Crawler.Core.Interfaces;

public interface ICrawlExecutionService
{
    Task ExecuteAsync(CrawlJobRequest request, Guid jobId, CancellationToken cancellationToken = default);
}