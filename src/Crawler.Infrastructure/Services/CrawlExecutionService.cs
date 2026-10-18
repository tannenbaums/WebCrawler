using System.Net;
using AngleSharp.Html.Parser;
using Crawler.Core.Interfaces;
using Crawler.Core.Models;
using Crawler.Core.Services;
using Microsoft.Extensions.Logging;

namespace Crawler.Infrastructure.Services;

public sealed class CrawlExecutionService : ICrawlExecutionService
{
    private readonly HttpClient _httpClient;
    private readonly ICrawlJobRepository _repository;
    private readonly ILogger<CrawlExecutionService> _logger;

    public CrawlExecutionService(HttpClient httpClient, ICrawlJobRepository repository, ILogger<CrawlExecutionService> logger)
    {
        _httpClient = httpClient;
        _repository = repository;
        _logger = logger;
    }

    public async Task ExecuteAsync(CrawlJobRequest request, Guid jobId, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateStatusAsync(jobId, CrawlJobStatus.Running, cancellationToken: cancellationToken);

        try
        {
            await CrawlAsync(request, jobId, cancellationToken);
            await _repository.UpdateStatusAsync(jobId, CrawlJobStatus.Completed, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _repository.UpdateStatusAsync(jobId, CrawlJobStatus.Canceled, "Crawl was canceled.", CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Crawl job {JobId} failed", jobId);
            await _repository.UpdateStatusAsync(jobId, CrawlJobStatus.Failed, exception.Message, CancellationToken.None);
        }
    }

    private async Task CrawlAsync(CrawlJobRequest request, Guid jobId, CancellationToken cancellationToken)
    {
        var startUri = new Uri(request.Url, UriKind.Absolute);
        if (startUri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Only HTTP and HTTPS URLs can be crawled.");
        }

        var startUrl = UrlCalculator.NormalizeUrl(startUri.AbsoluteUri);
        var pending = new Queue<(string Url, int Depth, long? ParentPageId)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue((startUrl, 0, null));
        visited.Add(startUrl);
        var parser = new HtmlParser();
        var processed = 0;

        while (pending.Count > 0 && processed < request.MaxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (url, depth, parentPageId) = pending.Dequeue();
            HttpResponseMessage? response = null;
            string html = string.Empty;
            int? statusCode = null;

            try
            {
                response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                statusCode = (int)response.StatusCode;
                if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType is "text/html" or "application/xhtml+xml")
                {
                    html = await response.Content.ReadAsStringAsync(cancellationToken);
                }
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Could not fetch {Url} in crawl job {JobId}", url, jobId);
            }
            finally
            {
                response?.Dispose();
            }

            var document = parser.ParseDocument(html);
            var links = GetLinks(document, new Uri(url));
            var internalLinks = links.Where(link => string.Equals(link.Host, startUri.Host, StringComparison.OrdinalIgnoreCase)).ToArray();
            var ratio = UrlCalculator.CalculateDomainLinkRatio(internalLinks.Length, links.Count);
            var pageId = await _repository.SavePageAsync(jobId, url, url, depth, ratio, statusCode, DateTimeOffset.UtcNow, cancellationToken);

            if (parentPageId.HasValue)
            {
                await _repository.SaveEdgeAsync(jobId, parentPageId.Value, pageId, cancellationToken);
            }

            await _repository.IncrementPagesProcessedAsync(jobId, cancellationToken);
            processed++;

            if (depth >= request.MaxDepth)
            {
                continue;
            }

            foreach (var link in internalLinks)
            {
                var normalized = UrlCalculator.NormalizeUrl(link.AbsoluteUri);
                if (visited.Add(normalized))
                {
                    pending.Enqueue((normalized, depth + 1, pageId));
                }
            }
        }
    }

    private static IReadOnlyList<Uri> GetLinks(AngleSharp.Dom.IDocument document, Uri baseUri)
    {
        var links = new List<Uri>();
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = WebUtility.HtmlDecode(anchor.GetAttribute("href"));
            if (Uri.TryCreate(baseUri, href, out var link) && link.Scheme is "http" or "https")
            {
                links.Add(link);
            }
        }

        return links;
    }
}