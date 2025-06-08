using Microsoft.AspNetCore.Mvc;
using Permafrost.EdgeService.Models;
using Permafrost.EdgeService.Services;
using System.ComponentModel.DataAnnotations;

namespace Permafrost.EdgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly ILogger<EventsController> _logger;

    public EventsController(IActiveDirectoryService activeDirectoryService, ILogger<EventsController> logger)
    {
        _activeDirectoryService = activeDirectoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get events from Active Directory
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 100, max: 1000)</param>
    /// <param name="eventType">Filter by event type</param>
    /// <param name="userName">Filter by username</param>
    /// <param name="computerName">Filter by computer name</param>
    /// <param name="fromDate">Filter events from date (ISO 8601 format)</param>
    /// <param name="toDate">Filter events to date (ISO 8601 format)</param>
    /// <param name="sortBy">Sort field</param>
    /// <param name="sortDescending">Sort direction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of events</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<DomainEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaginatedResponse<DomainEvent>>> GetEvents(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 1000)] int pageSize = 100,
        [FromQuery] string? eventType = null,
        [FromQuery] string? userName = null,
        [FromQuery] string? computerName = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate date range
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            {
                return BadRequest(new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "FromDate cannot be greater than ToDate" 
                });
            }

            var parameters = new EventQueryParameters
            {
                Page = page,
                PageSize = pageSize,
                EventType = eventType,
                UserName = userName,
                ComputerName = computerName,
                FromDate = fromDate,
                ToDate = toDate,
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            _logger.LogDebug("Getting events with parameters: Page={Page}, PageSize={PageSize}, EventType={EventType}, FromDate={FromDate}, ToDate={ToDate}", 
                page, pageSize, eventType, fromDate, toDate);

            var result = await _activeDirectoryService.GetEventsAsync(parameters, cancellationToken);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new ApiResponse<object> 
                    { 
                        Success = false, 
                        Message = result.Message ?? "Failed to retrieve events" 
                    });
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid parameters provided for GetEvents");
            return BadRequest(new ApiResponse<object> 
            { 
                Success = false, 
                Message = ex.Message 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "An error occurred while retrieving events" 
                });
        }
    }
}
