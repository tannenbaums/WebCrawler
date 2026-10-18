namespace Crawler.Core.Models;

public enum CrawlJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Canceled = 4
}
