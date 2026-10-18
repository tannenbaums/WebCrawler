namespace Crawler.Core.Models;

public record CrawlJobRequest
{
    public string Url { get; init; } = string.Empty;
    public int MaxDepth { get; init; } = 2;
    public int MaxPages { get; init; } = 200;
}
