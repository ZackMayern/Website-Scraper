using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ScraperApi.Models;

// Represents one scraped game record stored in MongoDB.
public sealed class GameListing
{
    // MongoDB document identity mapped to the ObjectId value.
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfDefault]
    public string? Id { get; set; }

    // Stable identifier from the source site used for deduplication.
    public int SourceId { get; set; }

    // Display name shown to API consumers.
    public string Title { get; set; } = string.Empty;

    // Genre tags captured from the source card.
    public List<string> Genres { get; set; } = [];

    // Description copied from the scraped listing.
    public string Description { get; set; } = string.Empty;

    // Parsed price value for filtering and sorting.
    public decimal Price { get; set; }

    // Original URL of the scraped listing.
    public string SourceUrl { get; set; } = string.Empty;

    // Timestamp indicating when the item was scraped.
    public DateTime ScrapedAt { get; set; }
}