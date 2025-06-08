using Permafrost.EdgeService.Models;

namespace Permafrost.EdgeService.Services;

public interface IEventHubService
{
    Task SendUserDataAsync(IEnumerable<DomainUser> users, CancellationToken cancellationToken = default);
    Task SendGroupDataAsync(IEnumerable<DomainGroup> groups, CancellationToken cancellationToken = default);
    Task SendEventDataAsync(IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default);
    Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
