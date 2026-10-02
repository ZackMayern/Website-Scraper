# ScraperApi

`ScraperApi` is an ASP.NET Core Web API that periodically collects product-style game listings from the Oxylabs sandbox site, stores them in MongoDB, and exposes a paginated query API.

The application is built with .NET 10, uses `HtmlAgilityPack` to parse HTML, and uses the MongoDB .NET driver for durable storage.

## Features

- Scrapes `https://sandbox.oxylabs.io/products`.
- Starts one scrape run when the application launches.
- Repeats the scrape every minute through a hosted background worker.
- Processes source pages 1 through 5, with a one-second delay between page requests.
- Parses source IDs, titles, genres, descriptions, prices, source URLs, and scrape timestamps.
- Upserts listings by source ID to prevent duplicate documents on later scrape runs.
- Provides paginated, genre-filtered, and price-filtered endpoints.
- Creates a unique MongoDB index on `SourceId` before persisting listings.
- Provides Swagger/OpenAPI documentation in the Development environment.
- Provides a lightweight health endpoint.

## Architecture

```text
Oxylabs Sandbox HTML
        |
        v
ScraperService
        |
        v
ScrapeWorker (startup, then every minute)
        |
        v
GameRepository
        |
        v
MongoDB: scraper_db.games
        |
        v
GamesController -> HTTP API consumers
```

| Area | Responsibility |
| --- | --- |
| `Program.cs` | Configures dependency injection, MongoDB, controllers, Swagger, HTTPS redirection, health checks, and the hosted worker. |
| `Services/ScrapeWorker.cs` | Runs the scheduled scrape, ensures indexes, saves listings, and logs individual page or persistence failures. |
| `Services/ScraperService.cs` | Downloads a sandbox product page and converts its HTML product cards into `GameListing` objects. |
| `Repositories/GameRepository.cs` | Owns MongoDB indexes, upserts, paginated queries, and lookup by source ID. |
| `Controllers/GamesController.cs` | Exposes the read API under `/api/games`. |
| `Models/GameListing.cs` | Defines the MongoDB document and API response shape. |

## Prerequisites

- .NET SDK 10.0 or later.
- A reachable MongoDB deployment, local or hosted.
- Network access to `https://sandbox.oxylabs.io` when scraping is enabled.

Verify the installed SDK:

```bash
dotnet --list-sdks
```

## Configuration

The application requires the `ConnectionStrings:MongoDb` setting. It creates and uses the `scraper_db` database and the `games` collection.

Set the connection string through an environment variable rather than committing a real connection string:

```bash
export ConnectionStrings__MongoDb='mongodb://localhost:27017'
```

For hosted MongoDB, provide the equivalent `mongodb+srv://...` URI supplied by the provider.

The checked-in settings file contains a placeholder pattern:

```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb+srv://username:password@cluster.example.com/?appName=ScraperApi"
  }
}
```

Do not commit credentials. If a real connection string has ever been written to a tracked or shared configuration file, rotate its password or access key in the MongoDB provider immediately, then replace it with an environment variable or local secret-store reference.

## Run Locally

Restore and run the API:

```bash
dotnet restore
dotnet run
```

The supplied launch profiles use these local URLs:

- HTTP: `http://localhost:5227`
- HTTPS: `https://localhost:7001`

When `ASPNETCORE_ENVIRONMENT=Development`, Swagger UI is available at:

```text
https://localhost:7001/swagger
```

The worker runs once at startup. Subsequent runs occur once per minute. A full run requests pages 1 through 5 and waits one second between pages, so its minimum request pacing delay is four seconds, excluding network, parsing, and database time.

## API Reference

### Health check

```http
GET /health
```

Example response:

```json
{
  "status": "Healthy",
  "utcTime": "2026-09-30T12:00:00.0000000Z"
}
```

### List games

```http
GET /api/games?page={page}&pageSize={pageSize}&genre={genre}&maxPrice={maxPrice}
```

Query parameters:

| Parameter | Type | Default | Constraints | Description |
| --- | --- | --- | --- | --- |
| `page` | integer | `1` | Must be at least `1`. | One-based page number. |
| `pageSize` | integer | `20` | Between `1` and `100`. | Maximum items per response page. |
| `genre` | string | omitted | Optional. | Returns listings containing the exact genre value. |
| `maxPrice` | decimal | omitted | Optional. | Returns listings priced at or below this value. |

Example request:

```bash
curl -k 'https://localhost:7001/api/games?page=1&pageSize=20&genre=Action&maxPrice=30.00'
```

Successful responses use this shape:

```json
{
  "page": 1,
  "pageSize": 20,
  "total": 42,
  "totalPages": 3,
  "items": [
    {
      "id": "68db...",
      "sourceId": 101,
      "title": "Example Game",
      "genres": ["Action", "Adventure"],
      "description": "Example description.",
      "price": 19.99,
      "sourceUrl": "https://sandbox.oxylabs.io/products/101",
      "scrapedAt": "2026-09-30T12:00:00Z"
    }
  ]
}
```

Invalid pagination values return `400 Bad Request`.

### Get a game by source ID

```http
GET /api/games/{sourceId}
```

Example request:

```bash
curl -k 'https://localhost:7001/api/games/101'
```

The endpoint returns `200 OK` with a `GameListing` when the source ID exists, or `404 Not Found` when it does not.

## Data Model

Each document in the `games` collection uses the following fields:

| Field | Type | Purpose |
| --- | --- | --- |
| `_id` / `Id` | MongoDB `ObjectId` | Database-generated document identifier. |
| `SourceId` | integer | Stable identifier parsed from the source URL; unique across the collection. |
| `Title` | string | Listing title extracted from the product card. |
| `Genres` | string array | Category labels extracted from the card. |
| `Description` | string | Listing description. |
| `Price` | decimal | Euro price parsed with German number formatting. |
| `SourceUrl` | string | Absolute URL of the source listing. |
| `ScrapedAt` | UTC timestamp | Time the listing was parsed. |

`Id` is nullable in the application model and omitted for new documents. MongoDB generates the `_id` during an upsert insert. Do not initialize an ObjectId-backed string property to an empty string because an empty string is not a valid MongoDB ObjectId.

## Scraping Behavior

`ScrapeWorker` creates a dependency-injection scope for each run, then:

1. Creates the unique `SourceId` index if needed.
2. Scrapes pages 1 through 5 from the Oxylabs sandbox product catalog.
3. Parses each card independently, logging malformed cards without abandoning the page.
4. Replaces the matching MongoDB document, or inserts it when the `SourceId` is new.
5. Continues after per-listing, per-page, and index-creation errors according to the worker's error handling.

The scraper expects product cards and their fields to keep the HTML classes currently used by the sandbox site. A markup change can cause individual cards or entire pages to be skipped, with details written to application logs.

## Build and Validate

Build the project:

```bash
dotnet build
```

Run with the Development configuration:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

After the application starts, verify its health endpoint:

```bash
curl -k https://localhost:7001/health
```

Then query listings after the startup scrape completes:

```bash
curl -k 'https://localhost:7001/api/games?page=1&pageSize=20'
```

## Dependencies

| Package | Version | Usage |
| --- | --- | --- |
| `HtmlAgilityPack` | `1.13.0` | HTML document and node parsing. |
| `MongoDB.Driver` | `3.11.2` | MongoDB connection, serialization, indexing, and queries. |
| `Swashbuckle.AspNetCore` | `10.2.1` | Development-time Swagger/OpenAPI UI and document generation. |

## Operational Notes

- The worker's schedule is currently defined in `ScrapeWorker.cs` as one minute. Change `ScrapeInterval` to adjust it.
- The number of pages is currently fixed at five in the worker loop. Change the loop bound to alter scrape coverage.
- Genre matching is an exact MongoDB array-element match. API consumers should use the same casing and spelling stored in `Genres`.
- Results are ordered by `SourceId` ascending before pagination.
- HTTPS redirection is enabled. Use the HTTPS profile for local API calls, or configure the deployment's HTTPS termination correctly.
- The project currently has no automated test project. Runtime validation should include a MongoDB-backed scrape, API request checks, and verification that upserts retain one document per `SourceId`.

## License

No license file is currently included in this repository.