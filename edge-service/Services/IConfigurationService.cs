using Permafrost.EdgeService.Models;

namespace Permafrost.EdgeService.Services;

public interface IConfigurationService
{
    ServiceConfiguration GetServiceConfiguration();
    ActiveDirectoryConfiguration GetActiveDirectoryConfiguration();
    EventHubConfiguration GetEventHubConfiguration();
    ApiConfiguration GetApiConfiguration();
    void UpdateServiceConfiguration(ServiceConfiguration configuration);
    Task SaveConfigurationAsync();
    Task ReloadConfigurationAsync();
}
