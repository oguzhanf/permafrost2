using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class ApplicationPermission
{
    public Guid ApplicationId { get; set; }
    
    public Guid PermissionId { get; set; }
    
    public Guid PrincipalId { get; set; } // User or Group ID
    
    [Required]
    [MaxLength(50)]
    public string PrincipalType { get; set; } = string.Empty; // "User" or "Group"
    
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ExpiresAt { get; set; }
    
    [MaxLength(256)]
    public string? GrantedBy { get; set; }
    
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty; // AD, Azure, M365
    
    public bool IsActive { get; set; } = true;
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    // Navigation properties
    public virtual Application Application { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}
