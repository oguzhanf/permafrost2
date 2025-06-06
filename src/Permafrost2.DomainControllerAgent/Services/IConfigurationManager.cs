using Permafrost2.Shared.DTOs;

namespace Permafrost2.DomainControllerAgent.Services;

public interface IConfigurationManager
{
    Task<Guid?> GetAgentIdAsync();
    Task SetAgentIdAsync(Guid agentId);
    Task<string?> GetApiKeyAsync();
    Task SetApiKeyAsync(string apiKey);
    Task<AgentConfigurationDto> GetConfigurationAsync();
    Task UpdateConfigurationAsync(AgentConfigurationDto configuration);
    Task<DateTime?> GetLastHeartbeatAsync();
    Task SetLastHeartbeatAsync(DateTime timestamp);
    Task<DateTime?> GetLastDataCollectionAsync();
    Task SetLastDataCollectionAsync(DateTime timestamp);
}
