# ScraperFront

ScraperFront is the Angular dashboard for ScraperApi. It loads up to 100 game listings and displays them in an AG Grid table with sorting, filtering, and client-side pagination. The summary reports the API total, loaded row count, and distinct genres in the loaded data.

## Requirements

- Node.js and npm
- The .NET SDK required by ScraperApi
- A working MongoDB connection configured for ScraperApi

## Run Locally

Start ScraperApi in a separate terminal. Its `https` launch profile listens on port `7001`, which is the target configured by the Angular development proxy. Configure the API's MongoDB connection string before starting it.

```bash
# Start ScraperApi with the HTTPS profile used by the frontend proxy.
cd ../ScraperApi
dotnet run --launch-profile https
```

In another terminal, from the ScraperFront directory, install frontend dependencies and start Angular:

```bash
# Install dependencies and start the dashboard with its /api proxy enabled.
npm install
npm start
```

Open [http://localhost:4200/](http://localhost:4200/). `npm start` serves the dashboard and proxies requests under `/api` to `https://localhost:7001` using [proxy.conf.json](proxy.conf.json). The development proxy accepts the local HTTPS development certificate. Because browser requests go to the Angular origin and are forwarded by the dev server, backend CORS is not required for this setup. The proxy is for local development; production hosting must route `/api` to ScraperApi separately.

## Build

Create an optimized production build in `dist/`:

```bash
# Build an optimized production version of ScraperFront.
npm run build
```

## Tests

Run the unit tests once with Vitest:

```bash
# Run the Angular unit tests once with Vitest.
npm test -- --watch=false
```

