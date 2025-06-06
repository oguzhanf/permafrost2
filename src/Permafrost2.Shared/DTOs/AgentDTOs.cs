using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Shared.DTOs;

public class AgentRegistrationRequest
{
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Type { get; set; } = string.Empty; // DomainController, Server, Workstation
    
    [Required]
    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(256)]
    public string MachineName { get; set; } = string.Empty;
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    [MaxLength(256)]
    public string? Domain { get; set; }
    
    [MaxLength(256)]
    public string? OperatingSystem { get; set; }
    
    public Dictionary<string, object>? Configuration { get; set; }
}

public class AgentRegistrationResponse
{
    public Guid AgentId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ApiKey { get; set; }
    public AgentConfigurationDto? Configuration { get; set; }
}

public class AgentHeartbeatRequest
{
    public Guid AgentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metrics { get; set; }
}

public class AgentHeartbeatResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public AgentConfigurationDto? UpdatedConfiguration { get; set; }
    public bool UpdateAvailable { get; set; }
    public string? UpdateVersion { get; set; }
    public string? UpdateUrl { get; set; }
}

public class AgentConfigurationDto
{
    public int HeartbeatIntervalSeconds { get; set; } = 300; // 5 minutes
    public int DataCollectionIntervalMinutes { get; set; } = 60; // 1 hour
    public bool EnableDetailedLogging { get; set; } = false;
    public List<string> EnabledDataTypes { get; set; } = new();
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

public class DataSubmissionRequest
{
    public Guid AgentId { get; set; }
    public string DataType { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; }
    public int RecordCount { get; set; }
    public string? Metadata { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string? DataHash { get; set; }
}

public class DataSubmissionResponse
{
    public Guid SubmissionId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTime ProcessedAt { get; set; }
}

public class DomainUserDto
{
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? Manager { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLogon { get; set; }
    public DateTime? PasswordLastSet { get; set; }
    public bool PasswordNeverExpires { get; set; }
    public bool AccountDisabled { get; set; }
    public bool AccountLocked { get; set; }
    public List<string> GroupMemberships { get; set; } = new();
    public string? DistinguishedName { get; set; }
    public string? ObjectSid { get; set; }
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();
}

public class AgentStatusDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Domain { get; set; }
    public bool IsActive { get; set; }
    public bool IsOnline { get; set; }
    public string? Status { get; set; }
    public string? StatusMessage { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public DateTime? LastDataCollection { get; set; }
}
