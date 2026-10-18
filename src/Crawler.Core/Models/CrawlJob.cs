namespace Crawler.Core.Models;

public class CrawlJob
{
    public Guid JobId { get; set; }
    public Guid CrawlId { get; set; }
    public string InputUrl { get; set; } = string.Empty;
    public CrawlJobStatus Status { get; set; } = CrawlJobStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
    public int PagesProcessed { get; set; }
}
