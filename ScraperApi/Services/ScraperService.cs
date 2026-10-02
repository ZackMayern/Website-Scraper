using System.Globalization;
using HtmlAgilityPack;
using ScraperApi.Models;

namespace ScraperApi.Services;

public sealed class ScraperService(HttpClient httpClient, ILogger<ScraperService> logger)
{
    // Scrape product pages from the sandbox site and parse them using the expected locale.
    private static readonly Uri ProductsBaseUri = new("https://sandbox.oxylabs.io");
    private static readonly CultureInfo EuropeanCulture = CultureInfo.GetCultureInfo("de-DE");

    // Download one page of product cards and convert them into GameListing objects.
    public async Task<List<GameListing>> ScrapePageAsync(int page)
    {
        // Reject invalid page numbers before performing any network work.
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

        // Build the page URL, fetch the HTML, and load it into an HTML parser.
        var requestUri = new Uri(ProductsBaseUri, $"products?page={page}");
        var html = await httpClient.GetStringAsync(requestUri);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        // Collect each product card into an in-memory list for later persistence.
        var cards = document.DocumentNode.SelectNodes(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' product-card ')]");
        var listings = new List<GameListing>(cards?.Count ?? 0);
        var scrapedAt = DateTime.UtcNow;

        // Return an empty list if the page has no product cards.
        if (cards is null)
        {
            return listings;
        }

        // Parse each card independently so one malformed item does not stop the page scrape.
        foreach (var card in cards)
        {
            try
            {
                listings.Add(ParseCard(card, scrapedAt));
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Skipping malformed game card on page {Page}.", page);
            }
        }

        return listings;
    }

    // Translate a single HTML product card into the domain model.
    private static GameListing ParseCard(HtmlNode card, DateTime scrapedAt)
    {
        // Read the source link and derive the external identifier from the URL path.
        var link = GetRequiredNode(
            card,
            ".//a[contains(concat(' ', normalize-space(@class), ' '), ' card-header ')]");
        var relativeUrl = link.GetAttributeValue("href", string.Empty);
        var sourceIdText = relativeUrl.TrimEnd('/').Split('/').LastOrDefault();

        // Parse the source identifier from the card URL.
        if (!int.TryParse(sourceIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var sourceId))
        {
            throw new FormatException($"Unable to parse a source ID from '{relativeUrl}'.");
        }

        // Read the genres and price text from the card and normalize the price using the expected culture.
        var genreNodes = card.SelectNodes(
            ".//p[contains(concat(' ', normalize-space(@class), ' '), ' category ')]/span");
        var priceText = GetNodeText(
            GetRequiredNode(
                card,
                ".//div[contains(concat(' ', normalize-space(@class), ' '), ' price-wrapper ')]"))
            .Replace("€", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!decimal.TryParse(priceText, NumberStyles.Number, EuropeanCulture, out var price))
        {
            throw new FormatException($"Unable to parse price '{priceText}'.");
        }

        // Map the HTML fields into the persisted game listing record.
        return new GameListing
        {
            SourceId = sourceId,
            Title = GetNodeText(
                GetRequiredNode(
                    card,
                    ".//h4[contains(concat(' ', normalize-space(@class), ' '), ' title ')]")),
            Genres = genreNodes?
                .Select(GetNodeText)
                .Where(genre => genre.Length > 0)
                .ToList() ?? [],
            Description = GetNodeText(
                GetRequiredNode(
                    card,
                    ".//p[contains(concat(' ', normalize-space(@class), ' '), ' description ')]")),
            Price = price,
            SourceUrl = new Uri(ProductsBaseUri, relativeUrl).ToString(),
            ScrapedAt = scrapedAt
        };
    }

    // Require a node to exist so parsing fails fast when the page structure changes.
    private static HtmlNode GetRequiredNode(HtmlNode parent, string xpath)
    {
        return parent.SelectSingleNode(xpath)
            ?? throw new FormatException($"Required element matching '{xpath}' was not found.");
    }

    // Decode HTML entities and trim whitespace from extracted text.
    private static string GetNodeText(HtmlNode node)
    {
        return HtmlEntity.DeEntitize(node.InnerText).Trim();
    }
}