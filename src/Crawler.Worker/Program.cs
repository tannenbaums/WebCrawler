using Crawler.Infrastructure.Settings;
using Crawler.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection(RabbitMqSettings.SectionName));

builder.Services.AddHostedService<CrawlWorker>();

var host = builder.Build();
host.Run();
