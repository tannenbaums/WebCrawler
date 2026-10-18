using Crawler.Core.Interfaces;
using Crawler.Core.Models;
using Crawler.Core.Services;
using Microsoft.Extensions.Logging;

namespace Crawler.Application.Services;

public sealed class CrawlExecutionService : ICrawlExecutionService
{
    private readonly ICrawlPageFetcher _pageFetcher;
    private readonly ICrawlJobRepository _repository;
    private readonly ILogger<CrawlExecutionService> _logger;

    public CrawlExecutionService(ICrawlPageFetcher pageFetcher, ICrawlJobRepository repository, ILogger<CrawlExecutionService> logger)
    {
        _pageFetcher = pageFetcher;
        _repository = repository;
        _logger = logger;
    }

    public async Task ExecuteAsync(CrawlJobRequest request, Guid jobId, CancellationToken cancellationToken = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["CrawlJobId"] = jobId });
        var startedAt = DateTimeOffset.UtcNow;
        var pagesProcessed = 0;
        _logger.LogInformation("Crawl started for {StartUrl} with depth {MaxDepth} and page limit {MaxPages}", request.Url, request.MaxDepth, request.MaxPages);

        try
        {
            await _repository.UpdateStatusAsync(jobId, CrawlJobStatus.Running, cancellationToken: cancellationToken);
            pagesProcessed = await CrawlAsync(request, jobId, cancellationToken);
            await _repository.UpdateStatusAsync(jobId, CrawlJobStatus.Completed, cancellationToken: cancellationToken);
            _logger.LogInformation("Crawl completed with {PagesProcessed} pages in {DurationMs} ms", pagesProcessed, (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _repository.UpdateStatusAsync(jobId, CrawlJobStatus.Canceled, "Crawl was canceled.", CancellationToken.None);
            _logger.LogWarning("Crawl canceled after processing {PagesProcessed} pages", pagesProcessed);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Crawl failed after processing {PagesProcessed} pages", pagesProcessed);
            await _repository.UpdateStatusAsync(jobId, CrawlJobStatus.Failed, exception.Message, CancellationToken.None);
        }
    }

    private async Task<int> CrawlAsync(CrawlJobRequest request, Guid jobId, CancellationToken cancellationToken)
    {
        var startUri = new Uri(request.Url, UriKind.Absolute);
        var startUrl = UrlCalculator.NormalizeUrl(startUri.AbsoluteUri);
        var pending = new Queue<(string Url, int Depth, long? ParentPageId)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startUrl };
        pending.Enqueue((startUrl, 0, null));
        var processed = 0;

        while (pending.Count > 0 && processed < request.MaxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (url, depth, parentPageId) = pending.Dequeue();
            IReadOnlyList<Uri> links = Array.Empty<Uri>();
            int? statusCode = null;

            try
            {
                var fetchedPage = await _pageFetcher.FetchAsync(url, cancellationToken);
                statusCode = fetchedPage.HttpStatusCode;
                links = fetchedPage.Links
                    .Select(link => Uri.TryCreate(link, UriKind.Absolute, out var parsed) ? parsed : null)
                    .Where(link => link is not null)
                    .Select(link => link!)
                    .ToArray();
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Page fetch failed for {PageUrl} at depth {Depth}", url, depth);
            }

            var sameDomainLinks = links
                .Where(link => string.Equals(link.Host, startUri.Host, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var ratio = UrlCalculator.CalculateDomainLinkRatio(sameDomainLinks.Length, links.Count);
            var pageId = await _repository.SavePageAsync(jobId, url, url, depth, ratio, statusCode, DateTimeOffset.UtcNow, cancellationToken);

            if (parentPageId.HasValue)
            {
                await _repository.SaveEdgeAsync(jobId, parentPageId.Value, pageId, cancellationToken);
            }

            await _repository.IncrementPagesProcessedAsync(jobId, cancellationToken);
            processed++;
            _logger.LogInformation("Crawled page {PageUrl} at depth {Depth}; HTTP {HttpStatusCode}; {LinkCount} links; domain ratio {DomainLinkRatio}", url, depth, statusCode, links.Count, ratio);

            if (depth >= request.MaxDepth)
            {
                continue;
            }

            foreach (var link in sameDomainLinks)
            {
                var normalized = UrlCalculator.NormalizeUrl(link.AbsoluteUri);
                if (visited.Add(normalized))
                {
                    pending.Enqueue((normalized, depth + 1, pageId));
                }
            }
        }

        return processed;
    }
}