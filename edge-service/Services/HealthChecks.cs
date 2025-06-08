using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Permafrost.EdgeService.Services;

public class ActiveDirectoryHealthCheck : IHealthCheck
{
    private readonly IActiveDirectoryService _activeDirectoryService;

    public ActiveDirectoryHealthCheck(IActiveDirectoryService activeDirectoryService)
    {
        _activeDirectoryService = activeDirectoryService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var healthStatus = await _activeDirectoryService.CheckHealthAsync(cancellationToken);
            
            if (healthStatus.IsHealthy)
            {
                return HealthCheckResult.Healthy(healthStatus.Message, healthStatus.Details);
            }
            else
            {
                return HealthCheckResult.Unhealthy(healthStatus.Message, data: healthStatus.Details);
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Active Directory health check failed", ex);
        }
    }
}

public class EventHubHealthCheck : IHealthCheck
{
    private readonly IEventHubService _eventHubService;

    public EventHubHealthCheck(IEventHubService eventHubService)
    {
        _eventHubService = eventHubService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var healthStatus = await _eventHubService.CheckHealthAsync(cancellationToken);
            
            if (healthStatus.IsHealthy)
            {
                return HealthCheckResult.Healthy(healthStatus.Message, healthStatus.Details);
            }
            else
            {
                return HealthCheckResult.Unhealthy(healthStatus.Message, data: healthStatus.Details);
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Event Hub health check failed", ex);
        }
    }
}
