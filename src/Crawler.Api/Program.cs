using Crawler.Core.Interfaces;
using Crawler.Core.Models;
using Crawler.Api;
using Crawler.Application.Services;
using Crawler.Infrastructure.Messaging;
using Crawler.Infrastructure.Repositories;
using Crawler.Infrastructure.Services;
using Crawler.Infrastructure.Settings;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection(RabbitMqSettings.SectionName));

builder.Services.AddSingleton<ICrawlJobRepository, DapperCrawlJobRepository>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddScoped<ICrawlExecutionService, CrawlExecutionService>();
builder.Services.AddHttpClient<ICrawlPageFetcher, HttpCrawlPageFetcher>();
builder.Services.AddSingleton<InProcessCrawlJobQueue>();
builder.Services.AddSingleton<ICrawlJobQueue>(services => services.GetRequiredService<InProcessCrawlJobQueue>());
builder.Services.AddHostedService<InProcessCrawlJobQueue>(services => services.GetRequiredService<InProcessCrawlJobQueue>());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("HttpRequest");
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? context.TraceIdentifier;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId,
        ["TraceId"] = context.TraceIdentifier
    });
    var timer = Stopwatch.StartNew();
    try
    {
        await next();
    }
    finally
    {
        logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms", context.Request.Method, context.Request.Path, context.Response.StatusCode, timer.Elapsed.TotalMilliseconds);
    }
});
app.UseHttpsRedirection();
app.MapControllers();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));

app.MapGet("/health/ready", async (ICrawlJobRepository repository, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    try
    {
        await repository.CheckDatabaseConnectionAsync(cancellationToken);
        return Results.Ok(new { status = "ready", checks = new { database = "ok" } });
    }
    catch (Exception exception)
    {
        loggerFactory.CreateLogger("Readiness").LogError(exception, "Database readiness check failed");
        return Results.Json(new { status = "not_ready", checks = new { database = "failed" } }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/health", async (ICrawlJobRepository repository, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    try
    {
        await repository.CheckDatabaseConnectionAsync(cancellationToken);
        return Results.Ok(new { status = "ready", checks = new { database = "ok" } });
    }
    catch (Exception exception)
    {
        loggerFactory.CreateLogger("Readiness").LogError(exception, "Database readiness check failed");
        return Results.Json(new { status = "not_ready", checks = new { database = "failed" } }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/jobs", async (int? take, ICrawlJobRepository repository, CancellationToken cancellationToken) =>
{
    var jobs = await repository.GetRecentAsync(take ?? 50, cancellationToken);
    return Results.Ok(jobs);
});

var useDirectExecution = builder.Configuration.GetValue("Execution:Direct", true);

app.MapPost("/api/jobs", async (CrawlJobRequest request, ICrawlJobRepository repository, IRabbitMqPublisher publisher, ICrawlJobQueue jobQueue, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
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
    var requestLogger = loggerFactory.CreateLogger("CrawlRequest");
    requestLogger.LogInformation("Accepted crawl job {CrawlJobId} for {StartUrl}", jobId, request.Url);
    if (useDirectExecution)
    {
        await jobQueue.EnqueueAsync(request, jobId, cancellationToken);
        return Results.Accepted($"/api/jobs/{jobId}", new { jobId, status = CrawlJobStatus.Pending.ToString() });
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
