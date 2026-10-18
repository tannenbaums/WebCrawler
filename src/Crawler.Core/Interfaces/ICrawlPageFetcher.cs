using Crawler.Core.Models;

namespace Crawler.Core.Interfaces;

public interface ICrawlPageFetcher
{
    Task<CrawlFetchedPage> FetchAsync(string url, CancellationToken cancellationToken = default);
}