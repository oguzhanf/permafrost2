namespace Permafrost2.DomainControllerAgent.UI.Services;

public interface IApiClient
{
    Task<bool> CheckConnectionAsync();
    Task<object?> GetAgentStatusAsync();
}
