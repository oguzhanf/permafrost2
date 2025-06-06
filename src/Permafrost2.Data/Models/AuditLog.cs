using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(256)]
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, Access
    
    [Required]
    [MaxLength(256)]
    public string EntityType { get; set; } = string.Empty; // User, Group, Application, etc.
    
    [Required]
    [MaxLength(256)]
    public string EntityId { get; set; } = string.Empty;
    
    [MaxLength(256)]
    public string? UserId { get; set; } // Who performed the action
    
    [MaxLength(256)]
    public string? UserName { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [MaxLength(4000)]
    public string? OldValues { get; set; } // JSON of old values
    
    [MaxLength(4000)]
    public string? NewValues { get; set; } // JSON of new values
    
    [MaxLength(256)]
    public string? IpAddress { get; set; }
    
    [MaxLength(512)]
    public string? UserAgent { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
}
