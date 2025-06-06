using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(256)]
    public string Username { get; set; } = string.Empty;
    
    [MaxLength(256)]
    public string? Email { get; set; }
    
    [MaxLength(256)]
    public string? DisplayName { get; set; }
    
    [MaxLength(256)]
    public string? FirstName { get; set; }
    
    [MaxLength(256)]
    public string? LastName { get; set; }
    
    [MaxLength(256)]
    public string? Department { get; set; }
    
    [MaxLength(256)]
    public string? JobTitle { get; set; }
    
    [MaxLength(256)]
    public string? Manager { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastUpdated { get; set; }
    
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty; // AD, Azure, M365
    
    [MaxLength(256)]
    public string? SourceId { get; set; } // External ID from source system
    
    // Navigation properties
    public virtual ICollection<UserGroupMembership> GroupMemberships { get; set; } = new List<UserGroupMembership>();
}
