using ScraperApi.Repositories;

namespace ScraperApi.Services;

public sealed class ScrapeWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ScrapeWorker> logger) : BackgroundService
{
    // Run the scraper every 1 minute and pause between page requests to avoid hammering the source site.
    private static readonly TimeSpan ScrapeInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PageDelay = TimeSpan.FromSeconds(1);

    // Start an immediate scrape run and then repeat it on a fixed timer.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunScrapeAsync(stoppingToken);

        using var timer = new PeriodicTimer(ScrapeInterval);

        // Keep scraping on the configured schedule until the host stops the worker.
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunScrapeAsync(stoppingToken);
        }
    }

    // Create a scoped repository/service pair, ensure indexes, and persist each scraped page.
    private async Task RunScrapeAsync(CancellationToken stoppingToken)
    {
        // Create a dependency scope so scoped services are resolved correctly inside the background worker.
        await using var scope = scopeFactory.CreateAsyncScope();
        var scraperService = scope.ServiceProvider.GetRequiredService<ScraperService>();
        var gameRepository = scope.ServiceProvider.GetRequiredService<GameRepository>();
        var savedCount = 0;

        // Make sure the unique index exists before writing any scraped data.
        try
        {
            await gameRepository.EnsureIndexesAsync(stoppingToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to ensure MongoDB indexes; skipping this scrape run.");
            return;
        }

        // Scrape the first five pages, saving each record as it is parsed.
        for (var page = 1; page <= 5; page++)
        {
            try
            {
                var listings = await scraperService.ScrapePageAsync(page);

                // Upsert each listing so reruns update existing records instead of duplicating them.
                foreach (var listing in listings)
                {
                    try
                    {
                        await gameRepository.UpsertAsync(listing);
                        savedCount++;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(
                            exception,
                            "Failed to save game {SourceId} scraped from page {Page}.",
                            listing.SourceId,
                            page);
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to scrape page {Page}.", page);
            }

            // Pause briefly between pages so the source site sees a steady request rate.
            if (page < 5)
            {
                await Task.Delay(PageDelay, stoppingToken);
            }
        }

        // Record how many listings were successfully persisted during this run.
        logger.LogInformation("Scrape run completed with {SavedCount} listings saved.", savedCount);
    }
}