namespace Crawler.Core.Models;

public class CrawlPage
{
    public long PageId { get; set; }
    public Guid CrawlId { get; set; }
    public string NormalizedUrl { get; set; } = string.Empty;
    public int? Depth { get; set; }
    public bool IsFetched { get; set; }
    public decimal? DomainLinkRatio { get; set; }
    public int? HttpStatusCode { get; set; }
    public DateTimeOffset? FetchedAt { get; set; }
}
