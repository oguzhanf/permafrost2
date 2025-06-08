using Microsoft.AspNetCore.Mvc;
using Permafrost.EdgeService.Models;
using Permafrost.EdgeService.Services;
using System.ComponentModel.DataAnnotations;

namespace Permafrost.EdgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IActiveDirectoryService activeDirectoryService, ILogger<UsersController> logger)
    {
        _activeDirectoryService = activeDirectoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get users from Active Directory
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 100, max: 1000)</param>
    /// <param name="filter">Filter by username, display name, or email</param>
    /// <param name="status">Filter by user status</param>
    /// <param name="department">Filter by department</param>
    /// <param name="sortBy">Sort field</param>
    /// <param name="sortDescending">Sort direction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of users</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<DomainUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaginatedResponse<DomainUser>>> GetUsers(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 1000)] int pageSize = 100,
        [FromQuery] string? filter = null,
        [FromQuery] UserStatus? status = null,
        [FromQuery] string? department = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new UserQueryParameters
            {
                Page = page,
                PageSize = pageSize,
                Filter = filter,
                Status = status,
                Department = department,
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            _logger.LogDebug("Getting users with parameters: Page={Page}, PageSize={PageSize}, Filter={Filter}", 
                page, pageSize, filter);

            var result = await _activeDirectoryService.GetUsersAsync(parameters, cancellationToken);
            
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
                        Message = result.Message ?? "Failed to retrieve users" 
                    });
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid parameters provided for GetUsers");
            return BadRequest(new ApiResponse<object> 
            { 
                Success = false, 
                Message = ex.Message 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "An error occurred while retrieving users" 
                });
        }
    }

    /// <summary>
    /// Get a specific user by Object GUID
    /// </summary>
    /// <param name="id">User Object GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DomainUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<DomainUser>>> GetUser(
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
                    Message = "User ID is required" 
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

            _logger.LogDebug("Getting user with ID: {UserId}", id);

            var user = await _activeDirectoryService.GetUserByIdAsync(id, cancellationToken);
            
            if (user == null)
            {
                return NotFound(new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "User not found" 
                });
            }

            return Ok(new ApiResponse<DomainUser> 
            { 
                Data = user, 
                Success = true, 
                Message = "User retrieved successfully" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new ApiResponse<object> 
                { 
                    Success = false, 
                    Message = "An error occurred while retrieving the user" 
                });
        }
    }
}
