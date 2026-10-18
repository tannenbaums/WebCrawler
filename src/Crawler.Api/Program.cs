using Crawler.Core.Interfaces;
using Crawler.Core.Models;
using Crawler.Infrastructure.Messaging;
using Crawler.Infrastructure.Repositories;
using Crawler.Infrastructure.Services;
using Crawler.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection(RabbitMqSettings.SectionName));

builder.Services.AddSingleton<ICrawlJobRepository, DapperCrawlJobRepository>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddHttpClient<ICrawlExecutionService, CrawlExecutionService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/jobs", async (int? take, ICrawlJobRepository repository, CancellationToken cancellationToken) =>
{
    var jobs = await repository.GetRecentAsync(take ?? 50, cancellationToken);
    return Results.Ok(jobs);
});

var useDirectExecution = builder.Configuration.GetValue("Execution:Direct", true);

app.MapPost("/api/jobs", async (CrawlJobRequest request, ICrawlJobRepository repository, IRabbitMqPublisher publisher, ICrawlExecutionService crawlService, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Url))
    {
        return Results.BadRequest(new { message = "Url is required." });
    }

    if (request.MaxDepth < 0 || request.MaxDepth > 10)
    {
        return Results.BadRequest(new { message = "MaxDepth must be between 0 and 10." });
    }

    if (request.MaxPages <= 0 || request.MaxPages > 10000)
    {
        return Results.BadRequest(new { message = "MaxPages must be between 1 and 10000." });
    }

    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var startUri) || startUri.Scheme is not ("http" or "https"))
    {
        return Results.BadRequest(new { message = "Url must be an absolute HTTP or HTTPS URL." });
    }

    var jobId = await repository.CreateAsync(request, cancellationToken);
    if (useDirectExecution)
    {
        await crawlService.ExecuteAsync(request, jobId, cancellationToken);
        var completedJob = await repository.GetByIdAsync(jobId, cancellationToken);
        var pages = await repository.GetPagesAsync(jobId, cancellationToken);
        var history = await repository.GetRecentAsync(cancellationToken: cancellationToken);
        return Results.Ok(new { job = completedJob, pages, history });
    }

    await publisher.PublishCrawlRequestAsync(request, jobId, cancellationToken);

    return Results.Accepted($"/api/jobs/{jobId}", new { jobId, status = CrawlJobStatus.Pending.ToString() });
});

app.MapGet("/api/jobs/{jobId:guid}", async (Guid jobId, ICrawlJobRepository repository, CancellationToken cancellationToken) =>
{
    var job = await repository.GetByIdAsync(jobId, cancellationToken);
    if (job is null)
    {
        return Results.NotFound();
    }

    var pages = await repository.GetPagesAsync(jobId, cancellationToken);
    return Results.Ok(new { job, pages });
});

app.Run();
