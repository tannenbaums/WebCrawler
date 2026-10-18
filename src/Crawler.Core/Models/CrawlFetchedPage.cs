namespace Crawler.Core.Models;

public sealed record CrawlFetchedPage(int? HttpStatusCode, IReadOnlyList<string> Links);