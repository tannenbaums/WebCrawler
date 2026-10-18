namespace Crawler.Infrastructure.Settings;

public class DatabaseSettings
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = "Server=localhost;Database=WebCrawler;Trusted_Connection=True;TrustServerCertificate=True;";
}
