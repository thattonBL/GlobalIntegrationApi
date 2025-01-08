using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace GlobalIntegrationApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DeleteEventController : Controller
{
    private readonly GlobalIntegrationContext _context;

    public DeleteEventController(GlobalIntegrationContext context)
    {
        _context = context;
    }

    [HttpDelete("{eventId}")]
    public async Task<IActionResult> DeleteEvent([FromRoute] Guid eventId)
    {
        try
        {
            // Find the parent event by EventId
            var parentEvent = await _context.IntegrationEventLogs
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (parentEvent == null)
            {
                return NotFound(new { success = false, message = "Event not found." });
            }

            // Deserialize the event content to find the identifier
            var content = JObject.Parse(parentEvent.Content);
            var identifier = content["RsiMessage"]?["Identifier"]?.ToString();

            if (string.IsNullOrEmpty(identifier))
            {
                return BadRequest(new { success = false, message = "Identifier not found in event content." });
            }

            // Find all events with the same identifier
            var eventsToDelete = await _context.IntegrationEventLogs
                .Where(e => e.EventId == eventId)
                .ToListAsync();

            if (eventsToDelete == null || !eventsToDelete.Any())
            {
                return NotFound(new { success = false, message = "No related events found for the given identifier." });
            }

            // Remove all related events
            _context.IntegrationEventLogs.RemoveRange(eventsToDelete);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Events deleted successfully." });
        }
        catch (Exception ex)
        {
            // Log the exception and return an error response
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
