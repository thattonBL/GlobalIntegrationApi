using GlobalIntegrationApi.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;

namespace GlobalIntegrationApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GetEventsController : Controller
{

    private readonly GlobalIntegrationContext _context;
    public GetEventsController(GlobalIntegrationContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetEvents(
    [FromQuery] int pageSize,
    [FromQuery] int pageNumber,
    [FromQuery] string? searchTerm,
    [FromQuery] string? startDate,
    [FromQuery] string? endDate,
    [FromQuery] string? sortColumn,
    [FromQuery] string? sortDirection)
    {
        // Query from database
        var query = _context.IntegrationEventLogs.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(e => e.Content.Contains(searchTerm));
        }

        // Apply date filters
        if (DateTime.TryParse(startDate, out DateTime startDateTime) &&
            DateTime.TryParse(endDate, out DateTime endDateTime))
        {
            query = query.Where(e => e.CreationTime.Date >= startDateTime.Date &&
                                      e.CreationTime.Date <= endDateTime.Date);
        }

        // Fetch data into memory
        var rawData = await query.ToListAsync();

        // Process data in memory
        var processedData = rawData
            .Select(e =>
            {
                var content = JObject.Parse(e.Content);
                return new
                {
                    Identifier = content["RsiMessage"]?["Identifier"]?.ToString() ??
                                 content["RsiMessageId"]?.ToString() ??
                                 content["Identifier"]?.ToString(),
                    ParentEvent = new
                    {
                        e.EventId,
                        e.CreationTime,
                        CollectionCode = content["RsiMessage"]?["CollectionCode"]?.ToString() ?? string.Empty,
                        Author = content["RsiMessage"]?["Author"]?.ToString() ?? string.Empty,
                        EventName = e.EventTypeName,
                        Title = content["RsiMessage"]?["Title"]?.ToString() ?? string.Empty
                    }
                };
            });

        // Apply sorting
        if (!string.IsNullOrEmpty(sortColumn))
        {
            try
            {
                var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "descending" : "ascending";
                processedData = processedData.AsQueryable().OrderBy($"{sortColumn} {direction}");
            }
            catch (ParseException ex)
            {
                return BadRequest(new { message = $"Invalid sort column: {sortColumn}.", details = ex.Message });
            }
        }

        // Get total count before pagination
        var totalRecords = processedData.Count();

        // Apply pagination
        var paginatedData = processedData
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new
        {
            totalRecords,
            data = paginatedData
        });
    }
}
