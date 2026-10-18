# WebCrawler

A local website crawler with a React dashboard, an ASP.NET Core API, SQL Server persistence, and a .NET worker project reserved for the queue-based architecture.

## Architecture

```text
React + Vite dashboard (frontend/)
        │ HTTP / Vite development proxy
        ▼
ASP.NET Core API (src/Crawler.Api/)
        ├── Crawl use case → Crawler.Application
        ├── Crawl job repository ── Dapper ── SQL Server (WebCrawler)
        └── RabbitMQ publisher (queue-based path retained)

Crawler.Application ── Core interfaces ── Infrastructure adapters (HTTP/HTML, SQL)

Shared contracts and models: src/Crawler.Core/
SQL and messaging implementations: src/Crawler.Infrastructure/
Worker host: src/Crawler.Worker/
Tests: tests/Crawler.Tests/
```

- **Crawler.Api** validates crawl requests, returns job details and history, and exposes the health endpoint.
- **Crawler.Core** contains shared models, service contracts, and URL calculations.
- **Crawler.Application** owns the crawl use case/orchestration and depends on Core ports, not SQL or HTTP implementations.
- **Crawler.Infrastructure** provides SQL Server persistence, HTTP/HTML page fetching, and RabbitMQ publishing adapters.
- **Crawler.Worker** preserves the separate worker host in the solution. Its current implementation is only a heartbeat; it does not yet consume RabbitMQ messages.
- **frontend** is the React/Vite crawl dashboard. It submits crawl requests and displays saved crawl history, discovered page trees, HTTP status codes, and Domain Link Ratios.

### Observability

The API writes structured JSON logs to stdout. HTTP logs include method, path, status code, elapsed time, trace ID, and correlation ID; crawl logs include the job ID, page URL/depth/status, page count, ratio, and total duration. Clients may supply `X-Correlation-ID`; the API echoes it in the response and includes it in the request log scope. Use `GET /health/live` to check that the process is alive and `GET /health/ready` (also available at `/health`) to check SQL Server connectivity.

## Requirements

- .NET 8 SDK
- Node.js and npm
- Local SQL Server with a database named `WebCrawler` and the expected schema (`Lookup`, `CrawlJobs`, `CrawlPages`, and `CrawlEdges`)
- No Docker or RabbitMQ is required for the current direct-execution path

The API reads its SQL connection string from `Database:ConnectionString`. Supply local credentials through the `Database__ConnectionString` environment variable; do not put real passwords in tracked appsettings files. The API uses SQL Server directly in direct-execution mode; the worker is not needed to run the UI or perform a direct crawl.

## Run locally

Open two PowerShell terminals from the repository root.

### 1. Start the API

```powershell
$env:Database__ConnectionString = "Server=localhost;Database=WebCrawler;Trusted_Connection=True;TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5088"
dotnet run --project .\src\Crawler.Api\Crawler.Api.csproj
```

The API should respond at `http://localhost:5088/health/ready` (database readiness), with liveness at `http://localhost:5088/health/live`. Development Swagger is at `http://localhost:5088/swagger`.

Direct execution is enabled by default in `src/Crawler.Api/appsettings.json` with `Execution:Direct` set to `true`. A crawl request runs in the API process and returns its result when finished. This is the temporary shortcut that bypasses RabbitMQ while keeping the queue and worker projects in the solution.

### 2. Start the dashboard

```powershell
Push-Location .\frontend
npm.cmd ci
npm.cmd run dev -- --host 127.0.0.1
```

Open **http://127.0.0.1:5173/**. Vite proxies `/api` and `/health` requests to the API at port 5088. Keep both terminals running while using the dashboard. If `node_modules` is already installed, `npm.cmd ci` can be skipped.

Enter a starting URL, choose a crawl depth and page limit, and select **Start crawling**. After completion, the dashboard shows the discovered-page tree and the saved crawl appears in **Crawl history**. Selecting a historical run loads its saved page tree.

### Sample API request

```powershell
$body = @{ url = "https://example.com"; maxDepth = 1; maxPages = 10 } | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5088/api/jobs -Method Post -ContentType "application/json" -Body $body
```

### API endpoints

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/health/live` | Process liveness check |
| `GET` | `/health/ready` or `/health` | SQL Server readiness check |
| `POST` | `/api/jobs` | Create and, in direct mode, execute a crawl |
| `GET` | `/api/jobs?take=50` | Return recent saved crawls (up to 100) |
| `GET` | `/api/jobs/{jobId}` | Return a job and its stored pages/relationships |

## Build and test

```powershell
dotnet build .\WebCrawler.slnx
dotnet test .\WebCrawler.slnx
Push-Location .\frontend
npm.cmd run lint
npm.cmd run build
Pop-Location
```

## CI/CD

The GitHub Actions workflow at `.github/workflows/ci.yml` runs on pushes to `main` and `QA`, and pull requests targeting `main` and `QA` (and can be run manually). It deliberately has no push or pull-request trigger for `Prod`; checking in or merging code to `Prod` will not start this workflow. It independently:

1. Restores, builds, and tests the .NET solution in Release configuration.
2. Installs the frontend from its lockfile, runs ESLint, and creates a production Vite build.

Treat a green CI run as the minimum merge gate: make small changes, push a feature branch, open a pull request, and fix pipeline failures before merging. Keep credentials and environment-specific connection strings out of source control. The workflow deliberately does not connect to the developer's local SQL Server or require RabbitMQ; database integration testing should use an isolated CI database once automated schema setup/migrations are in place.

### Branch-based QA and production promotion

Use environment branches to make releases visible and repeatable:

1. Develop on a short-lived feature branch and open a pull request into `main`. CI builds and tests the proposed change.
2. Promote a reviewed, green `main` commit into `QA` with a pull request. Once a QA deployment job is configured, updates to `QA` deploy to the QA environment for acceptance testing.
3. After QA sign-off, promote that same tested commit from `QA` to `Prod` with a pull request. Once configured, the production deployment job should require approval and deploy only from `Prod`.
4. If production validation fails, revert the promotion or redeploy the last known-good release.

The CI workflow validates only the exact branch names `main` and `QA`. Since you created `QA` from `main` and `Prod` from `QA`, promote tested changes along that path. `Prod` is intentionally excluded from automatic workflow triggers. Creating branches alone does not deploy the application. Add separately controlled deployment jobs only if desired, with GitHub Environments and protected secrets. Before deploying, establish environment-specific database configuration, schema migration/backup procedures, health checks, and rollback steps. Do not point a hosted API at `localhost`; use the database service reachable from that environment.

## Current execution-mode note

RabbitMQ configuration and the publisher remain in the architecture, but the current local setup does not require a broker. Set `Execution:Direct` to `false` only when restoring the queue-based path; a RabbitMQ consumer/crawler implementation in the worker still needs to be completed before that mode can process jobs.
