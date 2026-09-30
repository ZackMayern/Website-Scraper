using Microsoft.AspNetCore.Mvc;
using ScraperApi.Repositories;

namespace ScraperApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class GamesController(GameRepository gameRepository) : ControllerBase
{
    // Return a paged list of games with optional genre and price filters.
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? genre = null,
        [FromQuery] decimal? maxPrice = null)
    {
        // Reject invalid paging input before querying MongoDB.
        if (page < 1)
        {
            return BadRequest(new { error = "Page must be at least 1." });
        }

        // Keep page size within a safe range so responses stay predictable.
        if (pageSize is < 1 or > 100)
        {
            return BadRequest(new { error = "Page size must be between 1 and 100." });
        }

        // Query the repository and calculate how many pages the current result set spans.
        var (items, total) = await gameRepository.GetPagedAsync(page, pageSize, genre, maxPrice);
        var totalPages = total / pageSize + (total % pageSize == 0 ? 0 : 1);

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages,
            items
        });
    }

    // Return a single game listing by its source identifier.
    [HttpGet("{sourceId:int}")]
    public async Task<IActionResult> GetBySourceId(int sourceId)
    {
        var item = await gameRepository.GetBySourceIdAsync(sourceId);

        // Return 404 when the repository does not have a matching listing.
        return item is null ? NotFound() : Ok(item);
    }
}