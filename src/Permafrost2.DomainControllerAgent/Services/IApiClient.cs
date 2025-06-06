using Permafrost2.Shared.DTOs;

namespace Permafrost2.DomainControllerAgent.Services;

public interface IApiClient
{
    Task<AgentRegistrationResponse> RegisterAsync(AgentRegistrationRequest request);
    Task<AgentHeartbeatResponse> SendHeartbeatAsync(AgentHeartbeatRequest request);
    Task<DataSubmissionResponse> SubmitDataAsync(DataSubmissionRequest request);
    Task<bool> CheckConnectionAsync();
}
