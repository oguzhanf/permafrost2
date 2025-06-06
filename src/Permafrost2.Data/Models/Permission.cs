using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(100)]
    public string Type { get; set; } = string.Empty; // Read, Write, Admin, etc.
    
    [MaxLength(100)]
    public string? Level { get; set; } // Application, Resource, etc.
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastUpdated { get; set; }
    
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty; // AD, Azure, M365
    
    [MaxLength(256)]
    public string? SourceId { get; set; } // External ID from source system
    
    // Navigation properties
    public virtual ICollection<ApplicationPermission> ApplicationPermissions { get; set; } = new List<ApplicationPermission>();
}
