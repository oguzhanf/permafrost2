using Permafrost2.Data.Models;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.CustomerPortal.Services;

public interface IApplicationService
{
    Task<IEnumerable<Application>> GetApplicationsAsync();
    Task<Application?> GetApplicationByIdAsync(Guid id);
    Task<IEnumerable<ApplicationPermission>> GetApplicationPermissionsAsync(Guid applicationId);
    Task<IEnumerable<User>> GetApplicationUsersAsync(Guid applicationId);
    Task<IEnumerable<Group>> GetApplicationGroupsAsync(Guid applicationId);
    Task<Dictionary<string, int>> GetApplicationStatsAsync();
}

public interface IUserService
{
    Task<IEnumerable<User>> GetUsersAsync();
    Task<User?> GetUserByIdAsync(Guid id);
    Task<IEnumerable<Group>> GetUserGroupsAsync(Guid userId);
    Task<IEnumerable<ApplicationPermission>> GetUserPermissionsAsync(Guid userId);
    Task<Dictionary<string, int>> GetUserStatsAsync();
}

public interface IReportService
{
    Task<IEnumerable<ApplicationAccessReport>> GetApplicationAccessReportsAsync();
    Task<ApplicationAccessReport> GenerateApplicationAccessReportAsync(Guid applicationId);
    Task<UserAccessReport> GenerateUserAccessReportAsync(Guid userId);
    Task<SystemStatusReport> GetSystemStatusReportAsync();
    Task<IEnumerable<DataCollectionRun>> GetRecentDataCollectionRunsAsync(int count = 10);
}

public interface IAgentService
{
    Task<IEnumerable<AgentStatusDto>> GetAgentsAsync();
    Task<AgentStatusDto?> GetAgentByIdAsync(Guid agentId);
    Task<IEnumerable<AgentDataSubmission>> GetAgentSubmissionsAsync(Guid agentId, int limit = 50);
    Task<Dictionary<string, int>> GetAgentStatsAsync();
}

// DTOs for reports
public class ApplicationAccessReport
{
    public Guid ApplicationId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public int TotalUsers { get; set; }
    public int TotalGroups { get; set; }
    public int TotalPermissions { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<UserPermissionSummary> UserPermissions { get; set; } = new();
    public List<GroupPermissionSummary> GroupPermissions { get; set; } = new();
}

public class UserAccessReport
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int TotalApplications { get; set; }
    public int TotalGroups { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<ApplicationPermissionSummary> ApplicationPermissions { get; set; } = new();
    public List<GroupMembershipSummary> GroupMemberships { get; set; } = new();
}

public class SystemStatusReport
{
    public DateTime GeneratedAt { get; set; }
    public int TotalUsers { get; set; }
    public int TotalGroups { get; set; }
    public int TotalApplications { get; set; }
    public int TotalPermissions { get; set; }
    public List<DataSourceStatus> DataSources { get; set; } = new();
    public List<DataCollectionRun> RecentRuns { get; set; } = new();
}

public class UserPermissionSummary
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public DateTime GrantedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class GroupPermissionSummary
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public DateTime GrantedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class ApplicationPermissionSummary
{
    public Guid ApplicationId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public DateTime GrantedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class GroupMembershipSummary
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string GroupType { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class DataSourceStatus
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    public string Status => IsEnabled ? (LastRunStatus ?? "Unknown") : "Disabled";
}
