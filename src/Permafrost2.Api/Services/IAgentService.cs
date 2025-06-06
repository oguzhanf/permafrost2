using Permafrost2.Data.Models;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.Api.Services;

public interface IAgentService
{
    Task<AgentRegistrationResponse> RegisterAgentAsync(AgentRegistrationRequest request);
    Task<AgentHeartbeatResponse> ProcessHeartbeatAsync(AgentHeartbeatRequest request);
    Task<DataSubmissionResponse> ProcessDataSubmissionAsync(DataSubmissionRequest request);
    Task<IEnumerable<AgentStatusDto>> GetAgentsAsync();
    Task<AgentStatusDto?> GetAgentAsync(Guid agentId);
    Task<bool> UpdateAgentConfigurationAsync(Guid agentId, AgentConfigurationDto configuration);
    Task<bool> DeactivateAgentAsync(Guid agentId);
    Task<IEnumerable<AgentDataSubmission>> GetAgentSubmissionsAsync(Guid agentId, int limit = 50);
}
