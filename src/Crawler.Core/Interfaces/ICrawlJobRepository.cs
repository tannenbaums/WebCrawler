using Crawler.Core.Models;

namespace Crawler.Core.Interfaces;

public interface ICrawlJobRepository
{
    Task<Guid> CreateAsync(CrawlJobRequest request, CancellationToken cancellationToken = default);
    Task<CrawlJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid jobId, CrawlJobStatus status, string? failureReason = null, CancellationToken cancellationToken = default);
    Task IncrementPagesProcessedAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<long> SavePageAsync(Guid jobId, string url, string normalizedUrl, int depth, decimal domainLinkRatio, int? httpStatusCode, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default);
    Task SaveEdgeAsync(Guid jobId, long parentPageId, long childPageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrawlTreePage>> GetPagesAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrawlHistoryItem>> GetRecentAsync(int take = 50, CancellationToken cancellationToken = default);
}
