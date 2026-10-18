namespace Crawler.Infrastructure.Settings;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "crawl.requests";
    public string DeadLetterQueueName { get; set; } = "crawl.requests.dlq";
}
