namespace Crawler.Core.Models;

public sealed class CrawlTreePage
{
    public long PageId { get; init; }
    public long? ParentPageId { get; init; }
    public string Url { get; init; } = string.Empty;
    public string NormalizedUrl { get; init; } = string.Empty;
    public int Depth { get; init; }
    public decimal DomainLinkRatio { get; init; }
    public int? HttpStatusCode { get; init; }
}