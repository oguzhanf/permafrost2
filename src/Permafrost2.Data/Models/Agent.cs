using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
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
    
    [MaxLength(4000)]
    public string? Configuration { get; set; } // JSON configuration
    
    public bool IsActive { get; set; } = true;
    
    public bool IsOnline { get; set; } = false;
    
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastHeartbeat { get; set; }
    
    public DateTime? LastDataCollection { get; set; }
    
    [MaxLength(50)]
    public string? Status { get; set; } // Online, Offline, Error, Updating
    
    [MaxLength(1000)]
    public string? StatusMessage { get; set; }
    
    public DateTime? LastUpdated { get; set; }
    
    [MaxLength(256)]
    public string? UpdatedBy { get; set; }
}
