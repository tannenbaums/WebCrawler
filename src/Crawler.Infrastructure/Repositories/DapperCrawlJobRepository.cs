using Crawler.Core.Interfaces;
using Crawler.Core.Models;
using Crawler.Infrastructure.Settings;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Crawler.Infrastructure.Repositories;

public class DapperCrawlJobRepository : ICrawlJobRepository
{
    private readonly DatabaseSettings _databaseSettings;

    public DapperCrawlJobRepository(IOptions<DatabaseSettings> databaseSettings)
    {
        _databaseSettings = databaseSettings.Value;
    }

    public async Task<Guid> CreateAsync(CrawlJobRequest request, CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid();
        var statusId = await GetStatusIdAsync("Pending", cancellationToken);

        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            "INSERT INTO dbo.CrawlJobs (JobId, InputUrl, MaxDepth, CreatedAt, StartedAt, CompletedAt, FailureReason, PagesProcessed, StatusId) VALUES (@JobId, @InputUrl, @MaxDepth, @CreatedAt, NULL, NULL, NULL, 0, @StatusId);",
            new
            {
                JobId = jobId,
                InputUrl = request.Url,
                MaxDepth = request.MaxDepth,
                CreatedAt = DateTime.UtcNow,
                StatusId = statusId
            });

        return jobId;
    }

    public async Task<CrawlJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<CrawlJob>(
            @"SELECT j.JobId, j.InputUrl AS InputUrl, j.MaxDepth, j.CreatedAt, j.StartedAt, j.CompletedAt, j.FailureReason, j.PagesProcessed, l.Name AS Status
              FROM dbo.CrawlJobs j
              INNER JOIN dbo.Lookup l ON l.Id = j.StatusId
              WHERE j.JobId = @JobId;",
            new { JobId = jobId });
    }

    public async Task UpdateStatusAsync(Guid jobId, CrawlJobStatus status, string? failureReason = null, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var statusName = status.ToString();
        var statusId = await GetStatusIdAsync(statusName, cancellationToken);

        await connection.ExecuteAsync(
            @"UPDATE dbo.CrawlJobs
              SET StatusId = @StatusId,
                  FailureReason = @FailureReason,
                  StartedAt = COALESCE(StartedAt, @StartedAt),
                  CompletedAt = @CompletedAt
              WHERE JobId = @JobId;",
            new
            {
                JobId = jobId,
                StatusId = statusId,
                FailureReason = failureReason,
                StartedAt = status == CrawlJobStatus.Running ? (DateTime?)DateTime.UtcNow : null,
                CompletedAt = status == CrawlJobStatus.Completed || status == CrawlJobStatus.Failed || status == CrawlJobStatus.Canceled ? (DateTime?)DateTime.UtcNow : null
            });
    }

    public async Task IncrementPagesProcessedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            "UPDATE dbo.CrawlJobs SET PagesProcessed = PagesProcessed + 1 WHERE JobId = @JobId;",
            new { JobId = jobId });
    }

        public async Task<long> SavePageAsync(Guid jobId, string url, string normalizedUrl, int depth, decimal domainLinkRatio, int? httpStatusCode, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default)
        {
                using var connection = new SqlConnection(_databaseSettings.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                return await connection.ExecuteScalarAsync<long>(
                        @"INSERT INTO dbo.CrawlPages (JobId, Url, NormalizedUrl, Depth, DomainLinkRatio, HttpStatusCode, FetchedAt)
                            OUTPUT INSERTED.PageId
                            VALUES (@JobId, @Url, @NormalizedUrl, @Depth, @DomainLinkRatio, @HttpStatusCode, @FetchedAt);",
                        new { JobId = jobId, Url = url, NormalizedUrl = normalizedUrl, Depth = depth, DomainLinkRatio = domainLinkRatio, HttpStatusCode = httpStatusCode, FetchedAt = fetchedAt.UtcDateTime });
        }

        public async Task SaveEdgeAsync(Guid jobId, long parentPageId, long childPageId, CancellationToken cancellationToken = default)
        {
                using var connection = new SqlConnection(_databaseSettings.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                await connection.ExecuteAsync(
                        @"INSERT INTO dbo.CrawlEdges (JobId, ParentPageId, ChildPageId)
                            VALUES (@JobId, @ParentPageId, @ChildPageId);",
                        new { JobId = jobId, ParentPageId = parentPageId, ChildPageId = childPageId });
        }

    public async Task<IReadOnlyList<CrawlTreePage>> GetPagesAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var pages = await connection.QueryAsync<CrawlTreePage>(
            @"SELECT p.PageId, e.ParentPageId, p.Url, p.NormalizedUrl, p.Depth, p.DomainLinkRatio, p.HttpStatusCode
              FROM dbo.CrawlPages p
              LEFT JOIN dbo.CrawlEdges e ON e.JobId = p.JobId AND e.ChildPageId = p.PageId
              WHERE p.JobId = @JobId
              ORDER BY p.Depth, p.PageId;",
            new { JobId = jobId });

        return pages.AsList();
    }

    public async Task<IReadOnlyList<CrawlHistoryItem>> GetRecentAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var jobs = await connection.QueryAsync<CrawlHistoryItem>(
            @"SELECT TOP (@Take)
                  j.JobId,
                  j.InputUrl,
                  l.Name AS Status,
                  j.CreatedAt,
                  j.CompletedAt,
                  j.PagesProcessed,
                  j.MaxDepth
              FROM dbo.CrawlJobs j
              INNER JOIN dbo.Lookup l ON l.Id = j.StatusId
              ORDER BY j.CreatedAt DESC;",
            new { Take = Math.Clamp(take, 1, 100) });

        return jobs.AsList();
    }

    private async Task<int> GetStatusIdAsync(string statusName, CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(
            @"SELECT Id
              FROM dbo.Lookup
              WHERE Name = @StatusName
                AND ParentId = (SELECT Id FROM dbo.Lookup WHERE Name = 'Crawler.Status');",
            new { StatusName = statusName });
    }
}
