using Microsoft.AspNetCore.Mvc;
using Permafrost.EdgeService.Models;
using Permafrost.EdgeService.Services;
using System.ComponentModel.DataAnnotations;

namespace Permafrost.EdgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GroupsController : ControllerBase
{
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(IActiveDirectoryService activeDirectoryService, ILogger<GroupsController> logger)
    {
        _activeDirectoryService = activeDirectoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get groups from Active Directory
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 100, max: 1000)</param>
    /// <param name="filter">Filter by group name or display name</param>
    /// <param name="groupType">Filter by group type</param>
    /// <param name="distinguishedNameContains">Filter by distinguished name contains</param>
    /// <param name="sortBy">Sort field</param>
    /// <param name="sortDescending">Sort direction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of groups</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<DomainGroup>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaginatedResponse<DomainGroup>>> GetGroups(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 1000)] int pageSize = 100,
        [FromQuery] string? filter = null,
        [FromQuery] GroupType? groupType = null,
        [FromQuery] string? distinguishedNameContains = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new GroupQueryParameters
            {
                Page = page,
                PageSize = pageSize,
                Filter = filter,
                GroupType = groupType,
                DistinguishedNameContains = distinguishedNameContains,
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            _logger.LogDebug("Getting groups with parameters: Page={Page}, PageSize={PageSize}, Filter={Filter}", 
                page, pageSize, filter);

            var result = await _activeDirectoryService.GetGroupsAsync(parameters, cancellationToken);
            
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
                        Message = result.Message ?? "Failed to retrieve groups" 
                    });
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid parameters provided for GetGroups");
            return BadRequest(new ApiResponse<object> 
            { 
                Success = false, 
                Message = ex.Message 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving groups");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "An error occurred while retrieving groups" 
                });
        }
    }

    /// <summary>
    /// Get a specific group by Object GUID
    /// </summary>
    /// <param name="id">Group Object GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Group details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DomainGroup>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<DomainGroup>>> GetGroup(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "Group ID is required" 
                });
            }

            // Validate GUID format
            if (!Guid.TryParse(id, out _))
            {
                return BadRequest(new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "Invalid GUID format" 
                });
            }

            _logger.LogDebug("Getting group with ID: {GroupId}", id);

            var group = await _activeDirectoryService.GetGroupByIdAsync(id, cancellationToken);
            
            if (group == null)
            {
                return NotFound(new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "Group not found" 
                });
            }

            return Ok(new ApiResponse<DomainGroup> 
            { 
                Data = group, 
                Success = true, 
                Message = "Group retrieved successfully" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group {GroupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "An error occurred while retrieving the group" 
                });
        }
    }
}
