using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class Application
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(256)]
    public string? Url { get; set; }
    
    [MaxLength(100)]
    public string Type { get; set; } = string.Empty; // Web, Desktop, Service, etc.
    
    [MaxLength(256)]
    public string? Owner { get; set; }
    
    [MaxLength(100)]
    public string? Criticality { get; set; } // High, Medium, Low
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastUpdated { get; set; }
    
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty; // AD, Azure, M365
    
    [MaxLength(256)]
    public string? SourceId { get; set; } // External ID from source system
    
    // Navigation properties
    public virtual ICollection<ApplicationPermission> ApplicationPermissions { get; set; } = new List<ApplicationPermission>();
}
