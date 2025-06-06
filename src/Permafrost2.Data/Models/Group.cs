using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(100)]
    public string Type { get; set; } = string.Empty; // Security, Distribution, etc.
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastUpdated { get; set; }
    
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty; // AD, Azure, M365
    
    [MaxLength(256)]
    public string? SourceId { get; set; } // External ID from source system
    
    // Navigation properties
    public virtual ICollection<UserGroupMembership> UserMemberships { get; set; } = new List<UserGroupMembership>();
}
