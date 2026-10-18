using System.Net;
using AngleSharp.Html.Parser;
using Crawler.Core.Interfaces;
using Crawler.Core.Models;

namespace Crawler.Infrastructure.Services;

public sealed class HttpCrawlPageFetcher : ICrawlPageFetcher
{
    private readonly HttpClient _httpClient;
    private readonly HtmlParser _htmlParser = new();

    public HttpCrawlPageFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CrawlFetchedPage> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var statusCode = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType is not ("text/html" or "application/xhtml+xml"))
        {
            return new CrawlFetchedPage(statusCode, Array.Empty<string>());
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await _htmlParser.ParseDocumentAsync(html, cancellationToken);
        var baseUri = new Uri(url);
        var links = new List<string>();

        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = WebUtility.HtmlDecode(anchor.GetAttribute("href"));
            if (Uri.TryCreate(baseUri, href, out var link) && link.Scheme is "http" or "https")
            {
                links.Add(link.AbsoluteUri);
            }
        }

        return new CrawlFetchedPage(statusCode, links);
    }
}