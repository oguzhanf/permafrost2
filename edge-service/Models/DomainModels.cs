using System.Text.Json.Serialization;

namespace Permafrost.EdgeService.Models;

public class DomainUser
{
    public required string ObjectGuid { get; set; }
    public required string SamAccountName { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
    public string? ManagerDn { get; set; }
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public DateTime? LastLogon { get; set; }
    public DateTime? PasswordLastSet { get; set; }
    public DateTime? AccountExpires { get; set; }
    public DateTime? WhenCreated { get; set; }
    public DateTime? WhenChanged { get; set; }
    public string? DistinguishedName { get; set; }
    public List<string> MemberOf { get; set; } = new();
}

public class DomainGroup
{
    public required string ObjectGuid { get; set; }
    public required string SamAccountName { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public required string DistinguishedName { get; set; }
    public GroupType GroupType { get; set; }
    public DateTime? WhenCreated { get; set; }
    public DateTime? WhenChanged { get; set; }
    public List<string> Members { get; set; } = new();
}

public class DomainEvent
{
    public required string EventId { get; set; }
    public required string EventType { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? UserSid { get; set; }
    public string? UserName { get; set; }
    public string? ComputerName { get; set; }
    public string? SourceIp { get; set; }
    public string? LogonType { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GroupType
{
    Security,
    Distribution,
    Builtin
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserStatus
{
    Active,
    Disabled,
    Locked,
    Deleted
}

public class ApiResponse<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class PaginatedResponse<T> : ApiResponse<IEnumerable<T>>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class QueryParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public string? Filter { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = false;
}

public class UserQueryParameters : QueryParameters
{
    public UserStatus? Status { get; set; }
    public string? Department { get; set; }
    public DateTime? LastLogonAfter { get; set; }
    public DateTime? LastLogonBefore { get; set; }
}

public class GroupQueryParameters : QueryParameters
{
    public GroupType? GroupType { get; set; }
    public string? DistinguishedNameContains { get; set; }
}

public class EventQueryParameters : QueryParameters
{
    public string? EventType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? UserName { get; set; }
    public string? ComputerName { get; set; }
}

public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class ServiceConfiguration
{
    public string InstanceId { get; set; } = Environment.MachineName;
    public string DomainControllerName { get; set; } = Environment.MachineName;
    public int CollectionIntervalMinutes { get; set; } = 5;
    public bool EnableEventCollection { get; set; } = true;
    public bool EnableUserCollection { get; set; } = true;
    public bool EnableGroupCollection { get; set; } = true;
}

public class ActiveDirectoryConfiguration
{
    public required string DomainController { get; set; }
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public required string SearchBase { get; set; }
    public int QueryIntervalMinutes { get; set; } = 5;
    public int MaxResults { get; set; } = 1000;
}

public class EventHubConfiguration
{
    public required string ConnectionString { get; set; }
    public required string HubName { get; set; }
    public int BatchSize { get; set; } = 100;
    public int MaxWaitTimeSeconds { get; set; } = 30;
}

public class ApiConfiguration
{
    public int MaxPageSize { get; set; } = 1000;
    public int DefaultPageSize { get; set; } = 100;
    public bool EnableSwagger { get; set; } = true;
}
