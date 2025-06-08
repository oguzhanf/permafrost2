using Permafrost.EdgeService.Models;

namespace Permafrost.EdgeService.Services;

public interface IActiveDirectoryService
{
    Task<PaginatedResponse<DomainUser>> GetUsersAsync(UserQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<DomainGroup>> GetGroupsAsync(GroupQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<DomainEvent>> GetEventsAsync(EventQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<DomainUser?> GetUserByIdAsync(string objectGuid, CancellationToken cancellationToken = default);
    Task<DomainGroup?> GetGroupByIdAsync(string objectGuid, CancellationToken cancellationToken = default);
    Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
