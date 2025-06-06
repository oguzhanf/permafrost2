using Permafrost2.Shared.DTOs;

namespace Permafrost2.DomainControllerAgent.Services;

public interface IDomainDataCollector
{
    Task<List<DomainUserDto>> CollectUsersAsync();
    Task<List<object>> CollectGroupsAsync();
    Task<List<object>> CollectPoliciesAsync();
    Task<bool> TestDomainConnectionAsync();
}
