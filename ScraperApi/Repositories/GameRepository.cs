using MongoDB.Driver;
using ScraperApi.Models;

namespace ScraperApi.Repositories;

public sealed class GameRepository
{
    private readonly IMongoCollection<GameListing> _games;

    public GameRepository(IMongoDatabase database)
    {
        // Bind the repository to the games collection in the scraper database.
        _games = database.GetCollection<GameListing>("games");
    }

    // Ensure the source ID stays unique so each scraped game maps to one record.
    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<GameListing>(
            Builders<GameListing>.IndexKeys.Ascending(listing => listing.SourceId),
            new CreateIndexOptions { Unique = true });

        return _games.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    // Insert a new listing or replace the existing one with the same source ID.
    public Task UpsertAsync(GameListing listing)
    {
        var filter = Builders<GameListing>.Filter.Eq(
            existingListing => existingListing.SourceId,
            listing.SourceId);

        return _games.ReplaceOneAsync(
            filter,
            listing,
            new ReplaceOptions { IsUpsert = true });
    }

    // Build the optional filters, count matching documents, and return the requested page.
    public async Task<(IReadOnlyList<GameListing> Items, long Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? genre,
        decimal? maxPrice)
    {
        // Guard against invalid paging values even when the controller is bypassed.
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        // Assemble the MongoDB filter from the requested query parameters.
        var filters = new List<FilterDefinition<GameListing>>();

        if (!string.IsNullOrWhiteSpace(genre))
        {
            filters.Add(Builders<GameListing>.Filter.AnyEq(listing => listing.Genres, genre));
        }

        if (maxPrice.HasValue)
        {
            filters.Add(Builders<GameListing>.Filter.Lte(listing => listing.Price, maxPrice.Value));
        }

        var filter = filters.Count > 0
            ? Builders<GameListing>.Filter.And(filters)
            : Builders<GameListing>.Filter.Empty;

        var total = await _games.CountDocumentsAsync(filter);
        var items = await _games
            .Find(filter)
            .SortBy(listing => listing.SourceId)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // Fetch a single listing by the original source page identifier.
    public async Task<GameListing?> GetBySourceIdAsync(int sourceId)
    {
        return await _games
            .Find(listing => listing.SourceId == sourceId)
            .FirstOrDefaultAsync();
    }
}