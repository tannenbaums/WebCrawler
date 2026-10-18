namespace Crawler.Core.Models;

public class CrawlEdge
{
    public Guid CrawlId { get; set; }
    public long ParentPageId { get; set; }
    public long ChildPageId { get; set; }
}
