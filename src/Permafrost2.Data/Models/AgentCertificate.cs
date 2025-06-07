using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Data.Models;

public class AgentCertificate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid AgentId { get; set; }
    
    [Required]
    [MaxLength(256)]
    public string Thumbprint { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(256)]
    public string SerialNumber { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(1000)]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(1000)]
    public string Issuer { get; set; } = string.Empty;
    
    public DateTime NotBefore { get; set; }
    
    public DateTime NotAfter { get; set; }
    
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Revoked, Expired, Superseded
    
    [MaxLength(100)]
    public string Usage { get; set; } = "ClientAuthentication";
    
    public DateTime? RevokedAt { get; set; }
    
    [MaxLength(100)]
    public string? RevocationReason { get; set; }
    
    [MaxLength(4000)]
    public string? CertificateData { get; set; } // Base64 encoded certificate (optional storage)
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    // Navigation property
    public Agent? Agent { get; set; }
}
