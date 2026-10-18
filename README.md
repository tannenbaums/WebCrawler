# WebCrawler

A local website crawler with a React dashboard, an ASP.NET Core API, SQL Server persistence, and a .NET worker project reserved for the queue-based architecture.

## Architecture

```text
React + Vite dashboard (frontend/)
        │ HTTP / Vite development proxy
        ▼
ASP.NET Core API (src/Crawler.Api/)
        ├── Crawl execution service (direct mode, temporary local path)
        ├── Crawl job repository ── Dapper ── SQL Server (WebCrawler)
        └── RabbitMQ publisher (queue-based path retained)

Shared contracts and models: src/Crawler.Core/
SQL and messaging implementations: src/Crawler.Infrastructure/
Worker host: src/Crawler.Worker/
Tests: tests/Crawler.Tests/
```

- **Crawler.Api** validates crawl requests, returns job details and history, and exposes the health endpoint.
- **Crawler.Core** contains shared models, service contracts, and URL calculations.
- **Crawler.Infrastructure** contains SQL Server persistence, page parsing/crawling, and RabbitMQ publishing.
- **Crawler.Worker** preserves the separate worker host in the solution. Its current implementation is only a heartbeat; it does not yet consume RabbitMQ messages.
- **frontend** is the React/Vite crawl dashboard. It submits crawl requests and displays saved crawl history, discovered page trees, HTTP status codes, and Domain Link Ratios.

## Requirements

- .NET 8 SDK
- Node.js and npm
- Local SQL Server with a database named `WebCrawler` and the expected schema (`Lookup`, `CrawlJobs`, `CrawlPages`, and `CrawlEdges`)
- No Docker or RabbitMQ is required for the current direct-execution path

The API reads its SQL connection string from `Database:ConnectionString`. Set a local connection string in the API and worker appsettings files or override it with the `Database__ConnectionString` environment variable. Do not commit real database passwords to source control. The API uses SQL Server directly in direct-execution mode; the worker is not needed to run the UI or perform a direct crawl.

## Run locally

Open two PowerShell terminals from the repository root.

### 1. Start the API

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5088"
dotnet run --project .\src\Crawler.Api\Crawler.Api.csproj
```

The API should respond at `http://localhost:5088/health`. Development Swagger is at `http://localhost:5088/swagger`.

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
| `GET` | `/health` | API health check |
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

## Current execution-mode note

RabbitMQ configuration and the publisher remain in the architecture, but the current local setup does not require a broker. Set `Execution:Direct` to `false` only when restoring the queue-based path; a RabbitMQ consumer/crawler implementation in the worker still needs to be completed before that mode can process jobs.
