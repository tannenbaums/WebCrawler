namespace Crawler.Core.Models;

public sealed class CrawlHistoryItem
{
    public Guid JobId { get; init; }
    public string InputUrl { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int PagesProcessed { get; init; }
    public int MaxDepth { get; init; }
}