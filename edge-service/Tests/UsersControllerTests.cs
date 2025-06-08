using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Permafrost.EdgeService.Controllers;
using Permafrost.EdgeService.Models;
using Permafrost.EdgeService.Services;
using Xunit;

namespace Permafrost.EdgeService.Tests;

public class UsersControllerTests
{
    private readonly Mock<IActiveDirectoryService> _mockActiveDirectoryService;
    private readonly Mock<ILogger<UsersController>> _mockLogger;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _mockActiveDirectoryService = new Mock<IActiveDirectoryService>();
        _mockLogger = new Mock<ILogger<UsersController>>();
        _controller = new UsersController(_mockActiveDirectoryService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetUsers_WithValidParameters_ReturnsOkResult()
    {
        // Arrange
        var users = new List<DomainUser>
        {
            new DomainUser
            {
                ObjectGuid = Guid.NewGuid().ToString(),
                SamAccountName = "testuser1",
                DisplayName = "Test User 1",
                Email = "test1@example.com",
                Enabled = true
            },
            new DomainUser
            {
                ObjectGuid = Guid.NewGuid().ToString(),
                SamAccountName = "testuser2",
                DisplayName = "Test User 2",
                Email = "test2@example.com",
                Enabled = true
            }
        };

        var response = new PaginatedResponse<DomainUser>
        {
            Data = users,
            Success = true,
            Page = 1,
            PageSize = 100,
            TotalCount = 2,
            TotalPages = 1
        };

        _mockActiveDirectoryService
            .Setup(x => x.GetUsersAsync(It.IsAny<UserQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedResponse = Assert.IsType<PaginatedResponse<DomainUser>>(okResult.Value);
        Assert.True(returnedResponse.Success);
        Assert.Equal(2, returnedResponse.Data?.Count());
    }

    [Fact]
    public async Task GetUsers_WithServiceFailure_ReturnsInternalServerError()
    {
        // Arrange
        var response = new PaginatedResponse<DomainUser>
        {
            Data = Enumerable.Empty<DomainUser>(),
            Success = false,
            Message = "Service error",
            Page = 1,
            PageSize = 100,
            TotalCount = 0,
            TotalPages = 0
        };

        _mockActiveDirectoryService
            .Setup(x => x.GetUsersAsync(It.IsAny<UserQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<ApiResponse<object>>(statusResult.Value);
        Assert.False(errorResponse.Success);
        Assert.Equal("Service error", errorResponse.Message);
    }

    [Fact]
    public async Task GetUsers_WithException_ReturnsInternalServerError()
    {
        // Arrange
        _mockActiveDirectoryService
            .Setup(x => x.GetUsersAsync(It.IsAny<UserQueryParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<ApiResponse<object>>(statusResult.Value);
        Assert.False(errorResponse.Success);
        Assert.Contains("error occurred", errorResponse.Message);
    }

    [Fact]
    public async Task GetUser_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = new DomainUser
        {
            ObjectGuid = userId,
            SamAccountName = "testuser",
            DisplayName = "Test User",
            Email = "test@example.com",
            Enabled = true
        };

        _mockActiveDirectoryService
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.GetUser(userId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<DomainUser>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(userId, response.Data.ObjectGuid);
    }

    [Fact]
    public async Task GetUser_WithInvalidGuid_ReturnsBadRequest()
    {
        // Arrange
        var invalidId = "not-a-guid";

        // Act
        var result = await _controller.GetUser(invalidId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Invalid GUID", response.Message);
    }

    [Fact]
    public async Task GetUser_WithEmptyId_ReturnsBadRequest()
    {
        // Arrange
        var emptyId = "";

        // Act
        var result = await _controller.GetUser(emptyId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("required", response.Message);
    }

    [Fact]
    public async Task GetUser_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        _mockActiveDirectoryService
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainUser?)null);

        // Act
        var result = await _controller.GetUser(userId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.False(response.Success);
        Assert.Contains("not found", response.Message);
    }

    [Fact]
    public async Task GetUser_WithException_ReturnsInternalServerError()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        _mockActiveDirectoryService
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetUser(userId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<ApiResponse<object>>(statusResult.Value);
        Assert.False(errorResponse.Success);
        Assert.Contains("error occurred", errorResponse.Message);
    }

    [Theory]
    [InlineData(1, 100, null, null, null)]
    [InlineData(2, 50, "test", UserStatus.Active, "IT")]
    [InlineData(1, 1000, "", UserStatus.Disabled, "")]
    public async Task GetUsers_WithVariousParameters_CallsServiceWithCorrectParameters(
        int page, int pageSize, string? filter, UserStatus? status, string? department)
    {
        // Arrange
        var response = new PaginatedResponse<DomainUser>
        {
            Data = Enumerable.Empty<DomainUser>(),
            Success = true,
            Page = page,
            PageSize = pageSize,
            TotalCount = 0,
            TotalPages = 0
        };

        _mockActiveDirectoryService
            .Setup(x => x.GetUsersAsync(It.IsAny<UserQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        await _controller.GetUsers(page, pageSize, filter, status, department);

        // Assert
        _mockActiveDirectoryService.Verify(x => x.GetUsersAsync(
            It.Is<UserQueryParameters>(p => 
                p.Page == page && 
                p.PageSize == pageSize && 
                p.Filter == filter && 
                p.Status == status && 
                p.Department == department),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
