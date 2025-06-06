using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class UserGroupMembership
{
    public Guid UserId { get; set; }
    
    public Guid GroupId { get; set; }
    
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ExpiresAt { get; set; }
    
    [MaxLength(256)]
    public string? AssignedBy { get; set; }
    
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty; // AD, Azure, M365
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Group Group { get; set; } = null!;
}
