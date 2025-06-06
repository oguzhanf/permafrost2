using System.ServiceProcess;

namespace Permafrost2.DomainControllerAgent.UI.Services;

public interface IAgentServiceManager
{
    Task<ServiceControllerStatus> GetServiceStatusAsync();
    Task StartServiceAsync();
    Task StopServiceAsync();
    Task RestartServiceAsync();
    Task<string> GetServiceLogsAsync();
    Task<Dictionary<string, object>> GetServiceConfigurationAsync();
    Task UpdateServiceConfigurationAsync(Dictionary<string, object> configuration);
}
